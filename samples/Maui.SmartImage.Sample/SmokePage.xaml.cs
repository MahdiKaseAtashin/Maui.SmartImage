namespace Maui.SmartImage.Sample;

public partial class SmokePage : ContentPage
{
	public SmokePage()
	{
		InitializeComponent();

		// Improve Mac2 / XCTest discoverability of AutomationIds.
		AutomationProperties.SetIsInAccessibleTree(this, true);
		SemanticProperties.SetDescription(this, "Smoke.Page");
	}
}
