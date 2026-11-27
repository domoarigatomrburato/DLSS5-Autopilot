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
    /// <summary>The places outside the game folder a plan's layer and graphics preference steps change; null when there are none.</summary>
    public SharedPlaces? Shared { get; init; }

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
        return ResolveAsync(plan, paths, PriorInstall.Of(recorded, paths, logger), cancellationToken);
    }

    /// <summary>The plan made concrete over <paramref name="prior"/>, already read from the folder's record over <paramref name="paths"/> (the plan's game folder).</summary>
    /// <exception cref="IOException">A file could not be read.</exception>
    /// <exception cref="UnauthorizedAccessException">A file could not be opened.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public Task<ResolvedInstall> ResolveAsync(InstallPlan plan, InstallPaths paths, PriorInstall prior, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        SharedSteps? shared = Shared is null ? null : new(Shared, paths, prior, plan.Header, logger);
        return new Resolution(plan, paths, prior, logger, shared).RunAsync(cancellationToken);
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
        private readonly SharedSteps? _shared;
        private readonly HashSet<string> _named = [with(StringComparer.OrdinalIgnoreCase)];

        public Resolution(InstallPlan plan, InstallPaths paths, PriorInstall prior, ILogger logger, SharedSteps? shared)
        {
            _plan = plan;
            _paths = paths;
            _prior = prior;
            _backups = new(paths, prior, logger);
            _files = new(paths, prior, _backups);
            _shared = shared;
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

            List<InstallAction> released = await _backups.ReleasedAsync(_named, cancellationToken);
            if (released.Count > 0)
            {
                groups.Insert(0, new(Texts.Text("InstallStepSetAside"), released));
            }

            string installFolder = Path.GetFullPath(_plan.InstallFolder);
            return new(_paths, installFolder, Opening(installFolder), groups);
        }

        // The record the journal opens with, saying what the plan, the backups and the shared steps said.
        private InstallRecord Opening(string installFolder)
        {
            InstallRecord opening = _prior.Opening(_plan.Header, _paths.Relative(installFolder), _files.Carried) with
            {
                Notes = [.. _plan.Header.Notes, .. _backups.Notes],
            };
            return _shared?.Annotated(opening) ?? opening;
        }

        // A layer and a graphics preference are no entries of the game folder: they neither name nor keep one. The checks
        // refuse a plan with such a step and no places for it, which no arm takes.
        private Task<IReadOnlyList<InstallAction>> StepAsync(PlanStep step, CancellationToken cancellationToken) => step switch
        {
            RegisterLayer layer when _shared?.Layers is { } layers => layers.LayerAsync(layer, cancellationToken),
            SetGraphicsPreference preference when _shared is { } shared => shared.PreferenceAsync(preference),
            PlaceFile place => _files.PlaceAsync(place, Named(place), cancellationToken),
            MoveAside aside => _files.AsideAsync(aside, Named(aside), cancellationToken),
            CreateFolder folder => Task.FromResult<IReadOnlyList<InstallAction>>(_files.FoldersOf(Named(folder))),
            KeepEntry keep => Task.FromResult(_files.Keep(Named(keep))),
            PlacePattern pattern => Task.FromResult(Pattern(pattern)),
            _ => throw new NotSupportedException($"no resolution for {step.GetType().Name}"),
        };

        private IReadOnlyList<InstallAction> Pattern(PlacePattern pattern) => _files.Pattern(Named(pattern), _named);

        // The step's full path, which the plan now names.
        private string Named(PlanStep step)
        {
            string full = step.FullIn(_paths);
            _ = _named.Add(full);
            return full;
        }
    }
}
