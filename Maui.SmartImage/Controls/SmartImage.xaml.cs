using System.Diagnostics;
using System.Windows.Input;
using Maui.SmartImage.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Maui.SmartImage.Controls;

/// <summary>
/// Cache-aware, retryable image control with automatic local/remote source detection.
/// </summary>
public partial class SmartImage : ContentView
{
    /// <summary>Bindable property for <see cref="Source"/>.</summary>
    public static readonly BindableProperty SourceProperty = BindableProperty.Create(
        nameof(Source),
        typeof(string),
        typeof(SmartImage),
        null,
        BindingMode.OneWay,
        propertyChanged: SourcePropertyChanged);

    /// <summary>Bindable property for <see cref="Placeholder"/>.</summary>
    public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(
        nameof(Placeholder),
        typeof(ImageSource),
        typeof(SmartImage),
        null,
        BindingMode.OneWay,
        propertyChanged: PlaceholderOrErrorImageChanged);

    /// <summary>Bindable property for <see cref="ErrorImage"/>.</summary>
    public static readonly BindableProperty ErrorImageProperty = BindableProperty.Create(
        nameof(ErrorImage),
        typeof(ImageSource),
        typeof(SmartImage),
        null,
        BindingMode.OneWay,
        propertyChanged: PlaceholderOrErrorImageChanged);

    /// <summary>Bindable property for <see cref="KeepPreviousImageWhileLoading"/>.</summary>
    public static readonly BindableProperty KeepPreviousImageWhileLoadingProperty = BindableProperty.Create(
        nameof(KeepPreviousImageWhileLoading),
        typeof(bool),
        typeof(SmartImage),
        false,
        BindingMode.OneWay);

    /// <summary>Bindable property for <see cref="CachePolicy"/>.</summary>
    public static readonly BindableProperty CachePolicyProperty = BindableProperty.Create(
        nameof(CachePolicy),
        typeof(ImageCachePolicy),
        typeof(SmartImage),
        ImageCachePolicy.MemoryAndDisk,
        BindingMode.OneWay);

    /// <summary>Bindable property for <see cref="CacheDuration"/>.</summary>
    public static readonly BindableProperty CacheDurationProperty = BindableProperty.Create(
        nameof(CacheDuration),
        typeof(TimeSpan?),
        typeof(SmartImage),
        null,
        BindingMode.OneWay);

    /// <summary>Bindable property for <see cref="Timeout"/>.</summary>
    public static readonly BindableProperty TimeoutProperty = BindableProperty.Create(
        nameof(Timeout),
        typeof(TimeSpan?),
        typeof(SmartImage),
        null,
        BindingMode.OneWay);

    /// <summary>Bindable property for <see cref="MaxImageSizeBytes"/>.</summary>
    public static readonly BindableProperty MaxImageSizeBytesProperty = BindableProperty.Create(
        nameof(MaxImageSizeBytes),
        typeof(long?),
        typeof(SmartImage),
        null,
        BindingMode.OneWay);

    /// <summary>Bindable property for <see cref="MaxRetryCount"/>.</summary>
    public static readonly BindableProperty MaxRetryCountProperty = BindableProperty.Create(
        nameof(MaxRetryCount),
        typeof(int),
        typeof(SmartImage),
        2,
        BindingMode.OneWay,
        validateValue: (_, value) => value is int count && count >= 0);

    /// <summary>Bindable property for <see cref="EnableAutomaticRetry"/>.</summary>
    public static readonly BindableProperty EnableAutomaticRetryProperty = BindableProperty.Create(
        nameof(EnableAutomaticRetry),
        typeof(bool),
        typeof(SmartImage),
        true,
        BindingMode.OneWay);

    /// <summary>Bindable property for <see cref="RetryDelay"/>.</summary>
    public static readonly BindableProperty RetryDelayProperty = BindableProperty.Create(
        nameof(RetryDelay),
        typeof(TimeSpan),
        typeof(SmartImage),
        TimeSpan.FromSeconds(1),
        BindingMode.OneWay,
        validateValue: (_, value) => value is TimeSpan delay && delay >= TimeSpan.Zero);

    /// <summary>Bindable property for <see cref="EnableFadeAnimation"/>.</summary>
    public static readonly BindableProperty EnableFadeAnimationProperty = BindableProperty.Create(
        nameof(EnableFadeAnimation),
        typeof(bool),
        typeof(SmartImage),
        true,
        BindingMode.OneWay);

    /// <summary>Bindable property for <see cref="Aspect"/>.</summary>
    public static readonly BindableProperty AspectProperty = BindableProperty.Create(
        nameof(Aspect),
        typeof(Aspect),
        typeof(SmartImage),
        Aspect.AspectFill,
        BindingMode.OneWay,
        propertyChanged: AspectPropertyChanged);

    /// <summary>Bindable property for <see cref="RetryButtonText"/>.</summary>
    public static readonly BindableProperty RetryButtonTextProperty = BindableProperty.Create(
        nameof(RetryButtonText),
        typeof(string),
        typeof(SmartImage),
        "Retry",
        BindingMode.OneWay);

    /// <summary>Bindable property for <see cref="RetryOverlayBackgroundColor"/>.</summary>
    public static readonly BindableProperty RetryOverlayBackgroundColorProperty = BindableProperty.Create(
        nameof(RetryOverlayBackgroundColor),
        typeof(Color),
        typeof(SmartImage),
        Color.FromArgb("#E5E7EB"),
        BindingMode.OneWay);

    /// <summary>Bindable property for <see cref="RetryButtonTextColor"/>.</summary>
    public static readonly BindableProperty RetryButtonTextColorProperty = BindableProperty.Create(
        nameof(RetryButtonTextColor),
        typeof(Color),
        typeof(SmartImage),
        Color.FromArgb("#374151"),
        BindingMode.OneWay);

    /// <summary>Bindable property for <see cref="RetryButtonFontSize"/>.</summary>
    public static readonly BindableProperty RetryButtonFontSizeProperty = BindableProperty.Create(
        nameof(RetryButtonFontSize),
        typeof(double),
        typeof(SmartImage),
        12.0,
        BindingMode.OneWay);

    /// <summary>Bindable property for <see cref="SkeletonColor"/>.</summary>
    public static readonly BindableProperty SkeletonColorProperty = BindableProperty.Create(
        nameof(SkeletonColor),
        typeof(Color),
        typeof(SmartImage),
        Color.FromArgb("#E5E7EB"),
        BindingMode.OneWay);

    /// <summary>Bindable property for <see cref="SkeletonHighlightColor"/>.</summary>
    public static readonly BindableProperty SkeletonHighlightColorProperty = BindableProperty.Create(
        nameof(SkeletonHighlightColor),
        typeof(Color),
        typeof(SmartImage),
        Color.FromArgb("#F9FAFB"),
        BindingMode.OneWay);

    private static readonly BindablePropertyKey StatePropertyKey = BindableProperty.CreateReadOnly(
        nameof(State),
        typeof(SmartImageState),
        typeof(SmartImage),
        SmartImageState.Idle,
        propertyChanged: StatePropertyChanged);

    /// <summary>Bindable property for <see cref="State"/>.</summary>
    public static readonly BindableProperty StateProperty = StatePropertyKey.BindableProperty;

    private static readonly BindablePropertyKey ErrorPropertyKey = BindableProperty.CreateReadOnly(
        nameof(Error),
        typeof(string),
        typeof(SmartImage),
        null);

    /// <summary>Bindable property for <see cref="Error"/>.</summary>
    public static readonly BindableProperty ErrorProperty = ErrorPropertyKey.BindableProperty;

    private const string ShimmerAnimationName = "Shimmer";
    private const string MissingRegistrationError =
        "SmartImage requires builder.UseSmartImage() in MauiProgram.cs before remote sources can load.";

    private readonly LoadGenerationGuard _guard = new();
    private CancellationTokenSource? _loadCts;
    private IImageLoader? _imageLoader;
    private SmartImageOptions? _options;
    private ILogger<SmartImage> _logger = NullLogger<SmartImage>.Instance;
    private bool _missingRegistrationReported;

    /// <summary>
    /// Initializes a new <see cref="SmartImage"/>.
    /// </summary>
    public SmartImage()
    {
        RetryCommand = new Command(
            execute: () => _ = SafeRetryAsync(),
            canExecute: () => State != SmartImageState.Loading);
        InitializeComponent();
    }

    /// <summary>
    /// Local resource name, local file path, or http(s) URL.
    /// </summary>
    public string? Source
    {
        get => (string?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>
    /// Shown while idle/loading (unless <see cref="KeepPreviousImageWhileLoading"/>) and as a failure fallback.
    /// </summary>
    public ImageSource? Placeholder
    {
        get => (ImageSource?)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>
    /// Shown when <see cref="State"/> is <see cref="SmartImageState.Failed"/>.
    /// </summary>
    public ImageSource? ErrorImage
    {
        get => (ImageSource?)GetValue(ErrorImageProperty);
        set => SetValue(ErrorImageProperty, value);
    }

    /// <summary>
    /// When <see langword="true"/>, keeps the previously loaded image visible while a new load is in flight.
    /// </summary>
    public bool KeepPreviousImageWhileLoading
    {
        get => (bool)GetValue(KeepPreviousImageWhileLoadingProperty);
        set => SetValue(KeepPreviousImageWhileLoadingProperty, value);
    }

    /// <summary>
    /// Cache tiers used for remote sources.
    /// </summary>
    public ImageCachePolicy CachePolicy
    {
        get => (ImageCachePolicy)GetValue(CachePolicyProperty);
        set => SetValue(CachePolicyProperty, value);
    }

    /// <summary>
    /// Cache entry lifetime. When <see langword="null"/>, <see cref="SmartImageOptions.DefaultCacheDuration"/> is used.
    /// </summary>
    public TimeSpan? CacheDuration
    {
        get => (TimeSpan?)GetValue(CacheDurationProperty);
        set => SetValue(CacheDurationProperty, value);
    }

    /// <summary>
    /// Per-attempt download timeout. When <see langword="null"/>, <see cref="SmartImageOptions.DefaultTimeout"/> is used.
    /// </summary>
    public TimeSpan? Timeout
    {
        get => (TimeSpan?)GetValue(TimeoutProperty);
        set => SetValue(TimeoutProperty, value);
    }

    /// <summary>
    /// Maximum accepted download size. When <see langword="null"/>, <see cref="SmartImageOptions.DefaultMaxImageSizeBytes"/> is used.
    /// </summary>
    public long? MaxImageSizeBytes
    {
        get => (long?)GetValue(MaxImageSizeBytesProperty);
        set => SetValue(MaxImageSizeBytesProperty, value);
    }

    /// <summary>
    /// Number of automatic retries after the first attempt.
    /// </summary>
    public int MaxRetryCount
    {
        get => (int)GetValue(MaxRetryCountProperty);
        set => SetValue(MaxRetryCountProperty, value);
    }

    /// <summary>
    /// Whether transient remote failures should be retried automatically.
    /// </summary>
    public bool EnableAutomaticRetry
    {
        get => (bool)GetValue(EnableAutomaticRetryProperty);
        set => SetValue(EnableAutomaticRetryProperty, value);
    }

    /// <summary>
    /// Base delay for exponential backoff between automatic retries.
    /// </summary>
    public TimeSpan RetryDelay
    {
        get => (TimeSpan)GetValue(RetryDelayProperty);
        set => SetValue(RetryDelayProperty, value);
    }

    /// <summary>
    /// Whether to fade when swapping to a newly loaded remote image.
    /// </summary>
    public bool EnableFadeAnimation
    {
        get => (bool)GetValue(EnableFadeAnimationProperty);
        set => SetValue(EnableFadeAnimationProperty, value);
    }

    /// <summary>
    /// Aspect mode passed through to the inner <see cref="Image"/>.
    /// </summary>
    public Aspect Aspect
    {
        get => (Aspect)GetValue(AspectProperty);
        set => SetValue(AspectProperty, value);
    }

    /// <summary>
    /// Text of the built-in retry overlay.
    /// </summary>
    public string RetryButtonText
    {
        get => (string)GetValue(RetryButtonTextProperty);
        set => SetValue(RetryButtonTextProperty, value);
    }

    /// <summary>
    /// Background color of the built-in retry overlay.
    /// </summary>
    public Color RetryOverlayBackgroundColor
    {
        get => (Color)GetValue(RetryOverlayBackgroundColorProperty);
        set => SetValue(RetryOverlayBackgroundColorProperty, value);
    }

    /// <summary>
    /// Text color of the built-in retry overlay.
    /// </summary>
    public Color RetryButtonTextColor
    {
        get => (Color)GetValue(RetryButtonTextColorProperty);
        set => SetValue(RetryButtonTextColorProperty, value);
    }

    /// <summary>
    /// Font size of the built-in retry overlay text.
    /// </summary>
    public double RetryButtonFontSize
    {
        get => (double)GetValue(RetryButtonFontSizeProperty);
        set => SetValue(RetryButtonFontSizeProperty, value);
    }

    /// <summary>
    /// Base color of the shimmering skeleton placeholder.
    /// </summary>
    public Color SkeletonColor
    {
        get => (Color)GetValue(SkeletonColorProperty);
        set => SetValue(SkeletonColorProperty, value);
    }

    /// <summary>
    /// Highlight color of the shimmering skeleton placeholder.
    /// </summary>
    public Color SkeletonHighlightColor
    {
        get => (Color)GetValue(SkeletonHighlightColorProperty);
        set => SetValue(SkeletonHighlightColorProperty, value);
    }

    /// <summary>
    /// Current load state.
    /// </summary>
    public SmartImageState State
    {
        get => (SmartImageState)GetValue(StateProperty);
        private set => SetValue(StatePropertyKey, value);
    }

    /// <summary>
    /// Failure message when <see cref="State"/> is <see cref="SmartImageState.Failed"/>.
    /// </summary>
    public string? Error
    {
        get => (string?)GetValue(ErrorProperty);
        private set => SetValue(ErrorPropertyKey, value);
    }

    /// <summary>
    /// Command that re-attempts the current <see cref="Source"/>.
    /// </summary>
    public ICommand RetryCommand { get; }

    /// <summary>
    /// <see langword="true"/> when <see cref="State"/> is <see cref="SmartImageState.Loading"/>.
    /// </summary>
    public bool IsLoading => State == SmartImageState.Loading;

    /// <summary>
    /// <see langword="true"/> when <see cref="State"/> is <see cref="SmartImageState.Failed"/>.
    /// </summary>
    public bool IsFailed => State == SmartImageState.Failed;

    /// <inheritdoc />
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler is not null)
        {
            IServiceProvider? services = Handler.MauiContext?.Services;
            _imageLoader ??= services?.GetService<IImageLoader>();
            _options ??= services?.GetService<SmartImageOptions>();
            _logger = services?.GetService<ILogger<SmartImage>>() ?? NullLogger<SmartImage>.Instance;

            if (_imageLoader is null)
            {
                ReportMissingRegistration();
            }
            else
            {
                OnSourceChanged(Source);
            }
        }

        if (Handler is null)
        {
            _loadCts?.Cancel();
            _loadCts?.Dispose();
            _loadCts = null;
            StopShimmer();
        }
    }

    private void ReportMissingRegistration()
    {
        if (_missingRegistrationReported)
        {
            return;
        }

        _missingRegistrationReported = true;
        _logger.LogError(MissingRegistrationError);
        Debug.Fail(MissingRegistrationError);

        if (ImageSourceClassifier.Classify(Source) == ImageSourceKind.Remote)
        {
            ApplyFailed(MissingRegistrationError);
        }
    }

    private static void SourcePropertyChanged(BindableObject bindable, object oldValue, object? newValue)
    {
        if (bindable is SmartImage smartImage)
        {
            smartImage.OnSourceChanged((string?)newValue);
        }
    }

    private static void PlaceholderOrErrorImageChanged(BindableObject bindable, object oldValue, object? newValue)
    {
        if (bindable is not SmartImage smartImage)
        {
            return;
        }

        if (smartImage.State == SmartImageState.Idle)
        {
            smartImage.PART_Image.Source = smartImage.Placeholder;
        }
        else if (smartImage.State == SmartImageState.Failed)
        {
            smartImage.PART_Image.Source = smartImage.ErrorImage ?? smartImage.Placeholder;
        }
        else if (smartImage.State == SmartImageState.Loading && !smartImage.KeepPreviousImageWhileLoading)
        {
            smartImage.PART_Image.Source = smartImage.Placeholder;
        }
    }

    private static void AspectPropertyChanged(BindableObject bindable, object oldValue, object? newValue)
    {
        if (bindable is SmartImage { PART_Image: not null } smartImage && newValue is Aspect aspect)
        {
            smartImage.PART_Image.Aspect = aspect;
        }
    }

    private static void StatePropertyChanged(BindableObject bindable, object oldValue, object? newValue)
    {
        if (bindable is SmartImage smartImage)
        {
            smartImage.OnPropertyChanged(nameof(IsLoading));
            smartImage.OnPropertyChanged(nameof(IsFailed));
            ((Command)smartImage.RetryCommand).ChangeCanExecute();

            if (smartImage.State == SmartImageState.Loading)
            {
                smartImage.StartShimmer();
            }
            else
            {
                smartImage.StopShimmer();
            }
        }
    }

    private void StartShimmer()
    {
        PART_ShimmerHighlight.AbortAnimation(ShimmerAnimationName);
        RunShimmerCycle();
    }

    private void StopShimmer()
    {
        PART_ShimmerHighlight.AbortAnimation(ShimmerAnimationName);
    }

    private void RunShimmerCycle()
    {
        if (State != SmartImageState.Loading || Handler is null)
        {
            return;
        }

        double containerWidth = PART_SkeletonOverlay.Width > 0 ? PART_SkeletonOverlay.Width : 300;
        double highlightWidth = PART_ShimmerHighlight.Width > 0 ? PART_ShimmerHighlight.Width : 80;

        PART_ShimmerHighlight.TranslationX = -highlightWidth;

        Animation animation = new(v => PART_ShimmerHighlight.TranslationX = v, -highlightWidth, containerWidth + highlightWidth);
        animation.Commit(
            this,
            ShimmerAnimationName,
            length: 1200,
            easing: Easing.Linear,
            finished: (_, cancelled) =>
            {
                if (!cancelled && State == SmartImageState.Loading && Handler is not null)
                {
                    RunShimmerCycle();
                }
            });
    }

    private void OnSourceChanged(string? source)
    {
        long generation = _guard.Begin();

        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();

        _ = SafeLoadAsync(source, generation, _loadCts.Token);
    }

    private async Task SafeRetryAsync()
    {
        try
        {
            await RetryAsync().ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "SmartImage retry failed for source {Source}.", Source);
        }
    }

    private async Task SafeLoadAsync(string? source, long generation, CancellationToken cancellationToken)
    {
        try
        {
            await LoadAsync(source, generation, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Source changed or control detached.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SmartImage load failed for source {Source}.", source);

            if (_guard.IsCurrent(generation) && Handler is not null)
            {
                ApplyFailed(ex.Message);
            }
        }
    }

    private async Task RetryAsync()
    {
        if (State == SmartImageState.Loading)
        {
            return;
        }

        long generation = _guard.Begin();

        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();

        await LoadAsync(Source, generation, _loadCts.Token).ConfigureAwait(true);
    }

    private async Task LoadAsync(string? source, long generation, CancellationToken cancellationToken)
    {
        ImageSourceKind kind = ImageSourceClassifier.Classify(source);

        if (kind == ImageSourceKind.Empty)
        {
            if (_guard.IsCurrent(generation))
            {
                ApplyIdle();
            }

            return;
        }

        if (kind == ImageSourceKind.Local)
        {
            if (_guard.IsCurrent(generation))
            {
                ApplyLocal(source!);
            }

            return;
        }

        if (_imageLoader is null)
        {
            if (Handler is not null)
            {
                ReportMissingRegistration();
            }

            return;
        }

        if (!_guard.IsCurrent(generation))
        {
            return;
        }

        ApplyLoading();

        SmartImageOptions options = _options ?? new SmartImageOptions();

        ImageLoadRequest request = new()
        {
            Url = source!,
            CachePolicy = CachePolicy,
            CacheDuration = CacheDuration ?? options.DefaultCacheDuration,
            Timeout = Timeout,
            MaxImageSizeBytes = MaxImageSizeBytes ?? options.DefaultMaxImageSizeBytes,
            MaxRetryCount = MaxRetryCount,
            EnableAutomaticRetry = EnableAutomaticRetry,
            RetryDelay = RetryDelay
        };

        ImageLoadResult result;

        try
        {
            result = await _imageLoader.LoadAsync(request, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!_guard.IsCurrent(generation))
        {
            return;
        }

        if (result.IsSuccess)
        {
            await ApplyLoadedAsync(result.ImageData!, generation).ConfigureAwait(true);
        }
        else
        {
            ApplyFailed(result.ErrorMessage ?? result.ErrorKind?.ToString() ?? "Failed to load image.");
        }
    }

    private void ApplyIdle()
    {
        State = SmartImageState.Idle;
        Error = null;
        PART_Image.Source = Placeholder;
    }

    private void ApplyLocal(string source)
    {
        State = SmartImageState.Loaded;
        Error = null;
        PART_Image.Source = source;
    }

    private void ApplyLoading()
    {
        State = SmartImageState.Loading;
        Error = null;

        if (!KeepPreviousImageWhileLoading)
        {
            PART_Image.Source = Placeholder;
        }
    }

    private async Task ApplyLoadedAsync(byte[] imageData, long generation)
    {
        State = SmartImageState.Loaded;
        Error = null;

        ImageSource loadedSource = ImageSource.FromStream(_ => Task.FromResult<Stream>(new MemoryStream(imageData)));

        if (EnableFadeAnimation && Handler is not null)
        {
            try
            {
                await PART_Image.FadeToAsync(0, 100).ConfigureAwait(true);

                if (!_guard.IsCurrent(generation) || Handler is null)
                {
                    PART_Image.Opacity = 1;
                    return;
                }

                PART_Image.Source = loadedSource;
                await PART_Image.FadeToAsync(1, 150).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "SmartImage fade animation interrupted.");
                PART_Image.Opacity = 1;
                PART_Image.Source = loadedSource;
            }
        }
        else
        {
            PART_Image.Source = loadedSource;
            PART_Image.Opacity = 1;
        }
    }

    private void ApplyFailed(string errorMessage)
    {
        State = SmartImageState.Failed;
        Error = errorMessage;
        PART_Image.Source = ErrorImage ?? Placeholder;
    }
}
