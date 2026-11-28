using Autopilot.Core.Net;
using Autopilot.Windows.Signatures;

namespace Autopilot.Windows.Tests.Signatures;

/// <summary>What Windows says about a file's signature, on real files. Runs on Windows only.</summary>
[TestClass]
public sealed class AuthenticodeVerifierTests(TestContext testContext)
{
    private const string Microsoft = "Microsoft Corporation";

    private readonly AuthenticodeVerifier _verifier = new();

    [TestMethod]
    public async Task A_microsoft_binary_is_valid_and_signed_by_microsoft()
    {
        SignatureCheck check = await Check(SignedFiles.MicrosoftSigned);

        Assert.AreEqual(SignatureStatus.Valid, check.Status, check.Detail);
        Assert.AreEqual(Microsoft, check.Signer);
    }

    [TestMethod]
    public async Task A_file_checked_twice_in_a_row_is_valid_both_times()
    {
        SignatureCheck first = await Check(SignedFiles.MicrosoftSigned);
        SignatureCheck second = await Check(SignedFiles.MicrosoftSigned);

        Assert.AreEqual(new SignatureCheck(SignatureStatus.Valid, Microsoft, first.Detail), first);
        Assert.AreEqual(first, second);
    }

    [TestMethod]
    public async Task A_signed_download_still_named_part_is_valid()
    {
        using SignedFiles files = new();

        SignatureCheck check = await Check(files.Copy(SignedFiles.MicrosoftSigned, "0123456789abcdef-nvngx_dlss.dll.part"));

        Assert.AreEqual(SignatureStatus.Valid, check.Status, check.Detail);
        Assert.AreEqual(Microsoft, check.Signer);
    }

    [TestMethod]
    public async Task A_signed_file_changed_after_signing_is_invalid()
    {
        using SignedFiles files = new();

        SignatureCheck check = await Check(files.Tampered(SignedFiles.MicrosoftSigned, "tampered.exe"));

        Assert.AreEqual(SignatureStatus.Invalid, check.Status, check.Detail);
        Assert.IsNotNull(check.Detail);
    }

    [TestMethod]
    public async Task Our_own_unsigned_build_is_not_signed()
    {
        SignatureCheck check = await Check(SignedFiles.Unsigned);

        Assert.AreEqual(new SignatureCheck(SignatureStatus.NotSigned, null, check.Detail), check);
    }

    [TestMethod]
    public async Task A_text_file_is_not_signed()
    {
        using SignedFiles files = new();

        SignatureCheck check = await Check(files.Text("readme.txt", "not a program"));

        Assert.AreEqual(SignatureStatus.NotSigned, check.Status, check.Detail);
        Assert.IsNull(check.Signer);
    }

    [TestMethod]
    public async Task A_missing_file_is_unreadable_not_an_exception()
    {
        using SignedFiles files = new();

        SignatureCheck check = await Check(Path.Combine(files.Folder, "missing.dll"));

        Assert.AreEqual(SignatureStatus.Unreadable, check.Status);
        Assert.IsNull(check.Signer);
        Assert.IsNotNull(check.Detail);
    }

    [TestMethod]
    public async Task A_cancelled_check_throws()
    {
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => _verifier.CheckAsync(SignedFiles.MicrosoftSigned, cancelled.Token));
    }

    private Task<SignatureCheck> Check(string path) => _verifier.CheckAsync(path, testContext.CancellationToken);
}
