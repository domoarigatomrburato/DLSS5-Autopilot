using Autopilot.Core.Routes;

namespace Autopilot.Core.Tests.Routes;

/// <summary>The files and builds each setting's choices stand for, as the settings and the install name them.</summary>
[TestClass]
public sealed class SettingNamesTests
{
    [TestMethod]
    [DataRow(ReShadeProxy.Dxgi, "dxgi.dll")]
    [DataRow(ReShadeProxy.D3d11, "d3d11.dll")]
    [DataRow(ReShadeProxy.D3d12, "d3d12.dll")]
    [DataRow(ReShadeProxy.D3d10, "d3d10.dll")]
    [DataRow(ReShadeProxy.Opengl32, "opengl32.dll")]
    public void Each_name_reshade_loads_as_is_its_file(ReShadeProxy proxy, string file) => Assert.AreEqual(file, proxy.FileName());

    [TestMethod]
    [DataRow(OptiScalerProxy.Dxgi, "dxgi.dll")]
    [DataRow(OptiScalerProxy.Winmm, "winmm.dll")]
    [DataRow(OptiScalerProxy.Version, "version.dll")]
    [DataRow(OptiScalerProxy.Dbghelp, "dbghelp.dll")]
    [DataRow(OptiScalerProxy.D3d12, "d3d12.dll")]
    [DataRow(OptiScalerProxy.Wininet, "wininet.dll")]
    [DataRow(OptiScalerProxy.Winhttp, "winhttp.dll")]
    public void Each_name_optiscaler_loads_as_is_its_file(OptiScalerProxy proxy, string file) => Assert.AreEqual(file, proxy.FileName());

    [TestMethod]
    [DataRow(OwnFrameGeneration.Sm86, "version.dll, dlssg_sm86.ini", "dlssg_for_sm86")]
    [DataRow(OwnFrameGeneration.Sm86Winmm, "winmm.dll, dlssg_sm86.ini", "dlssg_for_sm86")]
    [DataRow(OwnFrameGeneration.Sm86Dxgi, "dxgi.dll, dlssg_sm86.ini", "dlssg_for_sm86")]
    [DataRow(OwnFrameGeneration.Sm86D3d12, "d3d12.dll, dlssg_sm86.ini", "dlssg_for_sm86")]
    [DataRow(OwnFrameGeneration.Sm86Dbghelp, "dbghelp.dll, dlssg_sm86.ini", "dlssg_for_sm86")]
    [DataRow(OwnFrameGeneration.Enabler, "dlss-enabler-headless.dll", "DLSS Enabler")]
    public void Own_frame_generation_files_go_in_under_their_names_dll_first(OwnFrameGeneration files, string names, string project)
    {
        Assert.AreEqual(names, string.Join(", ", files.FileNames()));
        Assert.AreEqual(project, files.Label());
    }

    [TestMethod]
    public void A_value_no_choice_stands_for_names_no_file()
    {
        _ = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ((ReShadeProxy)99).FileName());
        _ = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ((OptiScalerProxy)99).FileName());
        _ = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ((OwnFrameGeneration)99).FileNames());
    }

    [TestMethod]
    [DataRow(null, "")]
    [DataRow(OptiScalerBuildKey.Y4my4my4m, "y4my4my4m")]
    [DataRow(OptiScalerBuildKey.Wilsjo2, "wilsjo2")]
    [DataRow(OptiScalerBuildKey.Wilsjo2Mfg, "wilsjo2-mfg")]
    [DataRow(OptiScalerBuildKey.Janblade, "janblade")]
    [DataRow(OptiScalerBuildKey.Jlrouzies, "jlrouzies")]
    public void Each_optiscaler_build_choice_installs_its_build(OptiScalerBuildKey? key, string build) =>
        Assert.AreEqual(build, new RouteSettings { OptiScalerBuild = key }.OptiScaler.Key);

    [TestMethod]
    public void A_build_choice_no_build_has_is_never_read_as_the_default() =>
        _ = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new RouteSettings { OptiScalerBuild = (OptiScalerBuildKey)99 }.OptiScaler);

    [TestMethod]
    [DataRow(OwnFrameGeneration.Sm86, "sm86")]
    [DataRow(OwnFrameGeneration.Sm86Winmm, "sm86-winmm")]
    [DataRow(OwnFrameGeneration.Enabler, "enabler")]
    public void The_stored_copy_of_own_files_sits_under_the_sets_record_name(OwnFrameGeneration files, string folder) => Assert.AreEqual(folder, files.StoreFolder());
}
