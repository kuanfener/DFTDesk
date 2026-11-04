namespace DFTDesk.Tests;

public sealed class WindowsStartupRegistrationTests
{
    [Fact]
    public void LoginStartupCommandQuotesPathsWithSpacesAndStartsInTray()
    {
        var command = WindowsStartupRegistration.BuildCommand(@"C:\Program Files\DFTDesk\DFTDesk.exe");
        Assert.Equal("\"C:\\Program Files\\DFTDesk\\DFTDesk.exe\" --tray", command);
    }

    [Fact]
    public void LoginStartupCommandHandlesPortableExePath()
    {
        var command = WindowsStartupRegistration.BuildCommand(@"D:\Tools\DFTDesk.exe");
        Assert.Equal("\"D:\\Tools\\DFTDesk.exe\" --tray", command);
    }
}
