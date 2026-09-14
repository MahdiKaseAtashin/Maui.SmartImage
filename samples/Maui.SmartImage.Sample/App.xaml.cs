namespace Maui.SmartImage.Sample;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		if (IsSmokeMode())
		{
			return new Window(new SmokePage());
		}

		return new Window(new AppShell());
	}

	internal static bool IsSmokeMode()
	{
#if SMARTIMAGE_SMOKE
		return true;
#else
		string[] args = Environment.GetCommandLineArgs();
		foreach (string arg in args)
		{
			if (string.Equals(arg, "--smoke", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return string.Equals(
			Environment.GetEnvironmentVariable("SMARTIMAGE_SMOKE"),
			"1",
			StringComparison.Ordinal);
#endif
	}
}
