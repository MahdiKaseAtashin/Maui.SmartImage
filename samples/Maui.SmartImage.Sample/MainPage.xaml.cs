namespace Maui.SmartImage.Sample;

public partial class MainPage : ContentPage
{
	private static readonly string[] CycleUrls =
	[
		"https://picsum.photos/seed/cycle1/400",
		"https://picsum.photos/seed/cycle2/400",
		"https://picsum.photos/seed/cycle3/400",
		"https://picsum.photos/seed/cycle4/400",
	];

	private int cycleIndex;

	public MainPage()
	{
		InitializeComponent();
		CycleImage.KeepPreviousImageWhileLoading = KeepPreviousSwitch.IsToggled;
	}

	private void OnKeepPreviousToggled(object? sender, ToggledEventArgs e)
	{
		CycleImage.KeepPreviousImageWhileLoading = e.Value;
	}

	private void OnLoadNextImageClicked(object? sender, EventArgs e)
	{
		cycleIndex = (cycleIndex + 1) % CycleUrls.Length;
		CycleImage.Source = CycleUrls[cycleIndex];
	}
}
