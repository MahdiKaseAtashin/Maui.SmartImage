using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Maui.SmartImage.Services;

namespace Maui.SmartImage.Controls;

public partial class SmartImage : ContentView
{
    public static readonly BindableProperty SourceProperty = BindableProperty.Create(
        nameof(Source),
        typeof(string),
        typeof(SmartImage),
        null,
        BindingMode.OneWay,
        propertyChanged: SourcePropertyChanged);

    public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(
        nameof(Placeholder),
        typeof(ImageSource),
        typeof(SmartImage),
        null,
        BindingMode.OneWay);

    public static readonly BindableProperty ErrorImageProperty = BindableProperty.Create(
        nameof(ErrorImage),
        typeof(ImageSource),
        typeof(SmartImage),
        null,
        BindingMode.OneWay);

    public static readonly BindableProperty KeepPreviousImageWhileLoadingProperty = BindableProperty.Create(
        nameof(KeepPreviousImageWhileLoading),
        typeof(bool),
        typeof(SmartImage),
        false,
        BindingMode.OneWay);

    public static readonly BindableProperty CachePolicyProperty = BindableProperty.Create(
        nameof(CachePolicy),
        typeof(ImageCachePolicy),
        typeof(SmartImage),
        ImageCachePolicy.MemoryAndDisk,
        BindingMode.OneWay);

    public static readonly BindableProperty CacheDurationProperty = BindableProperty.Create(
        nameof(CacheDuration),
        typeof(TimeSpan?),
        typeof(SmartImage),
        null,
        BindingMode.OneWay);

    public static readonly BindableProperty TimeoutProperty = BindableProperty.Create(
        nameof(Timeout),
        typeof(TimeSpan?),
        typeof(SmartImage),
        null,
        BindingMode.OneWay);

    public static readonly BindableProperty MaxImageSizeBytesProperty = BindableProperty.Create(
        nameof(MaxImageSizeBytes),
        typeof(long?),
        typeof(SmartImage),
        null,
        BindingMode.OneWay);

    public static readonly BindableProperty MaxRetryCountProperty = BindableProperty.Create(
        nameof(MaxRetryCount),
        typeof(int),
        typeof(SmartImage),
        2,
        BindingMode.OneWay);

    public static readonly BindableProperty EnableAutomaticRetryProperty = BindableProperty.Create(
        nameof(EnableAutomaticRetry),
        typeof(bool),
        typeof(SmartImage),
        true,
        BindingMode.OneWay);

    public static readonly BindableProperty RetryDelayProperty = BindableProperty.Create(
        nameof(RetryDelay),
        typeof(TimeSpan),
        typeof(SmartImage),
        TimeSpan.FromSeconds(1),
        BindingMode.OneWay);

    public static readonly BindableProperty EnableFadeAnimationProperty = BindableProperty.Create(
        nameof(EnableFadeAnimation),
        typeof(bool),
        typeof(SmartImage),
        true,
        BindingMode.OneWay);

    public static readonly BindableProperty AspectProperty = BindableProperty.Create(
        nameof(Aspect),
        typeof(Aspect),
        typeof(SmartImage),
        Aspect.AspectFill,
        BindingMode.OneWay,
        propertyChanged: AspectPropertyChanged);

    public static readonly BindableProperty RetryButtonTextProperty = BindableProperty.Create(
        nameof(RetryButtonText),
        typeof(string),
        typeof(SmartImage),
        "Retry",
        BindingMode.OneWay);

    public static readonly BindableProperty RetryOverlayBackgroundColorProperty = BindableProperty.Create(
        nameof(RetryOverlayBackgroundColor),
        typeof(Color),
        typeof(SmartImage),
        Color.FromArgb("#E5E7EB"),
        BindingMode.OneWay);

    public static readonly BindableProperty RetryButtonTextColorProperty = BindableProperty.Create(
        nameof(RetryButtonTextColor),
        typeof(Color),
        typeof(SmartImage),
        Color.FromArgb("#374151"),
        BindingMode.OneWay);

    public static readonly BindableProperty RetryButtonFontSizeProperty = BindableProperty.Create(
        nameof(RetryButtonFontSize),
        typeof(double),
        typeof(SmartImage),
        12.0,
        BindingMode.OneWay);

    public static readonly BindableProperty SkeletonColorProperty = BindableProperty.Create(
        nameof(SkeletonColor),
        typeof(Color),
        typeof(SmartImage),
        Color.FromArgb("#E5E7EB"),
        BindingMode.OneWay);

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

    public static readonly BindableProperty StateProperty = StatePropertyKey.BindableProperty;

    private static readonly BindablePropertyKey ErrorPropertyKey = BindableProperty.CreateReadOnly(
        nameof(Error),
        typeof(string),
        typeof(SmartImage),
        null);

    public static readonly BindableProperty ErrorProperty = ErrorPropertyKey.BindableProperty;

    private const string ShimmerAnimationName = "Shimmer";

    private readonly LoadGenerationGuard _guard = new();
    private CancellationTokenSource? _loadCts;
    private IImageLoader? _imageLoader;

    public SmartImage()
    {
        RetryCommand = new AsyncRelayCommand(RetryAsync);
        InitializeComponent();
    }

    public string? Source
    {
        get => (string?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public ImageSource? Placeholder
    {
        get => (ImageSource?)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public ImageSource? ErrorImage
    {
        get => (ImageSource?)GetValue(ErrorImageProperty);
        set => SetValue(ErrorImageProperty, value);
    }

    public bool KeepPreviousImageWhileLoading
    {
        get => (bool)GetValue(KeepPreviousImageWhileLoadingProperty);
        set => SetValue(KeepPreviousImageWhileLoadingProperty, value);
    }

    public ImageCachePolicy CachePolicy
    {
        get => (ImageCachePolicy)GetValue(CachePolicyProperty);
        set => SetValue(CachePolicyProperty, value);
    }

    public TimeSpan? CacheDuration
    {
        get => (TimeSpan?)GetValue(CacheDurationProperty);
        set => SetValue(CacheDurationProperty, value);
    }

    public TimeSpan? Timeout
    {
        get => (TimeSpan?)GetValue(TimeoutProperty);
        set => SetValue(TimeoutProperty, value);
    }

    public long? MaxImageSizeBytes
    {
        get => (long?)GetValue(MaxImageSizeBytesProperty);
        set => SetValue(MaxImageSizeBytesProperty, value);
    }

    public int MaxRetryCount
    {
        get => (int)GetValue(MaxRetryCountProperty);
        set => SetValue(MaxRetryCountProperty, value);
    }

    public bool EnableAutomaticRetry
    {
        get => (bool)GetValue(EnableAutomaticRetryProperty);
        set => SetValue(EnableAutomaticRetryProperty, value);
    }

    public TimeSpan RetryDelay
    {
        get => (TimeSpan)GetValue(RetryDelayProperty);
        set => SetValue(RetryDelayProperty, value);
    }

    public bool EnableFadeAnimation
    {
        get => (bool)GetValue(EnableFadeAnimationProperty);
        set => SetValue(EnableFadeAnimationProperty, value);
    }

    public Aspect Aspect
    {
        get => (Aspect)GetValue(AspectProperty);
        set => SetValue(AspectProperty, value);
    }

    public string RetryButtonText
    {
        get => (string)GetValue(RetryButtonTextProperty);
        set => SetValue(RetryButtonTextProperty, value);
    }

    public Color RetryOverlayBackgroundColor
    {
        get => (Color)GetValue(RetryOverlayBackgroundColorProperty);
        set => SetValue(RetryOverlayBackgroundColorProperty, value);
    }

    public Color RetryButtonTextColor
    {
        get => (Color)GetValue(RetryButtonTextColorProperty);
        set => SetValue(RetryButtonTextColorProperty, value);
    }

    public double RetryButtonFontSize
    {
        get => (double)GetValue(RetryButtonFontSizeProperty);
        set => SetValue(RetryButtonFontSizeProperty, value);
    }

    public Color SkeletonColor
    {
        get => (Color)GetValue(SkeletonColorProperty);
        set => SetValue(SkeletonColorProperty, value);
    }

    public Color SkeletonHighlightColor
    {
        get => (Color)GetValue(SkeletonHighlightColorProperty);
        set => SetValue(SkeletonHighlightColorProperty, value);
    }

    public SmartImageState State
    {
        get => (SmartImageState)GetValue(StateProperty);
        private set => SetValue(StatePropertyKey, value);
    }

    public string? Error
    {
        get => (string?)GetValue(ErrorProperty);
        private set => SetValue(ErrorPropertyKey, value);
    }

    public ICommand RetryCommand { get; }

    public bool IsLoading => State == SmartImageState.Loading;

    public bool IsFailed => State == SmartImageState.Failed;

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler is not null && _imageLoader is null)
        {
            _imageLoader = Handler.MauiContext?.Services.GetService<IImageLoader>();

            // Source may have already been set (e.g. via a binding) before this control had a Handler,
            // in which case the earlier load attempt was deferred. Re-run it now that the loader is available.
            if (_imageLoader is not null)
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

    private static void SourcePropertyChanged(BindableObject bindable, object oldValue, object? newValue)
    {
        if (bindable is SmartImage smartImage)
        {
            smartImage.OnSourceChanged((string?)newValue);
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
                if (!cancelled)
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

        _ = LoadAsync(source, generation, _loadCts.Token);
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
            // No Handler/DI context yet; OnHandlerChanged will retry once the loader becomes available.
            return;
        }

        if (!_guard.IsCurrent(generation))
        {
            return;
        }

        ApplyLoading();

        ImageLoadRequest request = new()
        {
            Url = source!,
            CachePolicy = CachePolicy,
            CacheDuration = CacheDuration,
            Timeout = Timeout,
            MaxImageSizeBytes = MaxImageSizeBytes,
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
            // Source changed again (or the control was detached) before this load finished; abandon quietly.
            return;
        }

        if (!_guard.IsCurrent(generation))
        {
            // A newer load has already started; this result is stale and must never overwrite it.
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

        if (EnableFadeAnimation)
        {
            await PART_Image.FadeToAsync(0, 100).ConfigureAwait(true);

            if (!_guard.IsCurrent(generation))
            {
                PART_Image.Opacity = 1;
                return;
            }

            PART_Image.Source = loadedSource;
            await PART_Image.FadeToAsync(1, 150).ConfigureAwait(true);
        }
        else
        {
            PART_Image.Source = loadedSource;
        }
    }

    private void ApplyFailed(string errorMessage)
    {
        State = SmartImageState.Failed;
        Error = errorMessage;
        PART_Image.Source = ErrorImage ?? Placeholder;
    }
}
