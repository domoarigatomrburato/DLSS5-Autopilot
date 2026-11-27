using Autopilot.Core.Resources;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Install;

/// <summary>
/// Turns an install plan, the game folder and its current record into the concrete actions per path, with
/// the hashes it read: read-only, no write and no request, so the preview and the transaction see one answer.
/// </summary>
/// <param name="logger">Where record entries that lead outside the game folder are logged.</param>
internal sealed partial class InstallResolver(ILogger logger)
{
    /// <summary>
    /// The plan made concrete over <paramref name="recorded"/>, which is the folder's record (null when none);
    /// only an <see cref="InstallState.Installed"/> one is the previous install. The plan has passed
    /// <see cref="InstallChecks"/>.
    /// </summary>
    /// <exception cref="IOException">A file could not be read.</exception>
    /// <exception cref="UnauthorizedAccessException">A file could not be opened.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public Task<ResolvedInstall> ResolveAsync(InstallPlan plan, RecordedInstall? recorded, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        InstallPaths paths = new(plan.GameFolder);
        return new Resolution(plan, paths, PriorInstall.Of(recorded, paths, logger), logger).RunAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The install record names {Path}, which leads outside the game folder: ignored")]
    internal static partial void LogOutside(ILogger logger, string path);

    private sealed class Resolution
    {
        private readonly InstallPlan _plan;
        private readonly InstallPaths _paths;
        private readonly PriorInstall _prior;
        private readonly BackupDecisions _backups;
        private readonly FileSteps _files;
        private readonly HashSet<string> _named = [with(StringComparer.OrdinalIgnoreCase)];

        public Resolution(InstallPlan plan, InstallPaths paths, PriorInstall prior, ILogger logger)
        {
            _plan = plan;
            _paths = paths;
            _prior = prior;
            _backups = new(paths, prior, logger);
            _files = new(paths, prior, _backups);
        }

        public async Task<ResolvedInstall> RunAsync(CancellationToken cancellationToken)
        {
            List<ResolvedGroup> groups = [];
            foreach (PlanGroup group in _plan.Groups)
            {
                List<InstallAction> actions = [];
                foreach (PlanStep step in group.Steps)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    actions.AddRange(await StepAsync(step, cancellationToken));
                }

                groups.Add(new(group.Name, actions));
            }

            List<InstallAction> released = await ReleasedAsync(cancellationToken);
            if (released.Count > 0)
            {
                groups.Insert(0, new(Texts.Text("InstallStepSetAside"), released));
            }

            string installFolder = Path.GetFullPath(_plan.InstallFolder);
            InstallRecord opening = _prior.Opening(_plan.Header, _paths.Relative(installFolder), _files.Carried) with
            {
                Notes = [.. _plan.Header.Notes, .. _backups.Notes],
            };
            return new(_paths, installFolder, opening, groups);
        }

        // The previous install's entries the plan does not name.
        private async Task<List<InstallAction>> ReleasedAsync(CancellationToken cancellationToken)
        {
            List<InstallAction> released = [];
            foreach ((string full, Prior before) in _prior.Entries.Where(entry => !_named.Contains(entry.Key)))
            {
                released.AddRange(await _backups.ReleaseAsync(full, before, cancellationToken));
            }

            return released;
        }

        private Task<IReadOnlyList<InstallAction>> StepAsync(PlanStep step, CancellationToken cancellationToken)
        {
            string full = _paths.Full(step.Path);
            _ = _named.Add(full);
            return step switch
            {
                PlaceFile place => _files.PlaceAsync(place, full, cancellationToken),
                MoveAside aside => _files.AsideAsync(aside, full, cancellationToken),
                CreateFolder => Task.FromResult<IReadOnlyList<InstallAction>>(_files.FoldersOf(full)),
                KeepEntry => Task.FromResult(_files.Keep(full)),
                _ => throw new NotSupportedException($"no resolution for {step.GetType().Name}"),
            };
        }
    }
}
