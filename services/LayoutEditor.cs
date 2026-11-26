using navigationshurely.models;
using navigationshurely.terminal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;

namespace navigationshurely.services
{
    /// <summary>
    /// Coordinates selection and mouse editing for registered MainCanvas
    /// regions. All geometry is read from and written to the active profile's
    /// Config objects through ShiftManager.
    /// </summary>
    public sealed class LayoutEditor
    {
        private const double HandleSize = 12;
        private const double FontScaleMinimum = 0.35;
        private const double FontScaleMaximum = 4.0;

        private readonly Canvas _canvas;
        private readonly Canvas _overlay;
        private readonly Rectangle _outline;
        private readonly System.Windows.Controls.TextBlock _statusText;
        private readonly FrameworkElement _guidedHudEditSurface;
        private readonly Dictionary<LayoutResizeHandle, Rectangle> _handles;
        private readonly Dictionary<Controls, FrameworkElement> _regions = new();
        private readonly Dictionary<Controls, Panel> _regionPanels = new();
        private readonly Dictionary<Panel, Brush?> _originalPanelBackgrounds = new();
        private readonly Dictionary<TextBlock, Brush?> _originalTextBlockBackgrounds = new();
        private readonly Dictionary<FrameworkElement, Controls> _editSurfaces = new();
        private readonly ShiftManager _shiftManager;
        private readonly bool _originalCanvasFocusable;

        private Controls? _selectedControl;
        private Config? _observedConfig;
        private DragSession? _dragSession;
        private bool _isEnabled;

        public bool IsEnabled => _isEnabled;
        public Controls? SelectedControl => _selectedControl;

        public LayoutEditor(
            Canvas canvas,
            Canvas overlay,
            Rectangle outline,
            System.Windows.Controls.TextBlock statusText,
            FrameworkElement guidedHudEditSurface,
            IEnumerable<KeyValuePair<LayoutResizeHandle, Rectangle>> handles,
            ShiftManager shiftManager)
        {
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
            _outline = outline ?? throw new ArgumentNullException(nameof(outline));
            _statusText = statusText ?? throw new ArgumentNullException(nameof(statusText));
            _guidedHudEditSurface = guidedHudEditSurface ??
                throw new ArgumentNullException(nameof(guidedHudEditSurface));
            _shiftManager = shiftManager ?? throw new ArgumentNullException(nameof(shiftManager));
            _originalCanvasFocusable = _canvas.Focusable;

            _handles = handles?.ToDictionary(pair => pair.Key, pair => pair.Value)
                ?? throw new ArgumentNullException(nameof(handles));

            _canvas.PreviewMouseLeftButtonDown += Canvas_PreviewMouseLeftButtonDown;
            _canvas.PreviewMouseMove += Canvas_PreviewMouseMove;
            _canvas.PreviewMouseLeftButtonUp += Canvas_PreviewMouseLeftButtonUp;
            _canvas.PreviewMouseRightButtonDown += Canvas_PreviewMouseRightButtonDown;
            _canvas.PreviewMouseRightButtonUp += Canvas_PreviewMouseRightButtonUp;

            _overlay.Visibility = Visibility.Collapsed;
            _guidedHudEditSurface.Visibility = Visibility.Collapsed;
        }

        public void RegisterRegion(Controls control, FrameworkElement element)
        {
            ArgumentNullException.ThrowIfNull(element);

            if (!Enum.IsDefined(typeof(Controls), control))
                throw new ArgumentOutOfRangeException(nameof(control));

            if (_regions.ContainsKey(control))
                throw new InvalidOperationException(
                    $"A layout region for '{control}' is already registered.");

            _regions.Add(control, element);
            element.SizeChanged += RegisteredRegion_SizeChanged;

            if (element is Panel panel)
            {
                _regionPanels.Add(control, panel);
                _originalPanelBackgrounds.TryAdd(panel, panel.Background);
            }

            if (element is TextBlock textBlock)
                _originalTextBlockBackgrounds.TryAdd(textBlock, textBlock.Background);
        }

        public void RegisterEditSurface(Controls control, FrameworkElement element)
        {
            ArgumentNullException.ThrowIfNull(element);

            if (!_regions.ContainsKey(control))
                throw new InvalidOperationException(
                    $"Register region '{control}' before its edit surface.");

            if (_editSurfaces.ContainsKey(element))
                throw new InvalidOperationException(
                    "An edit surface can only be registered once.");

            _editSurfaces.Add(element, control);
        }

        public void SetEditMode(bool enabled)
        {
            if (_isEnabled == enabled)
                return;

            _isEnabled = enabled;
            CancelDrag();

            // Empty portions of configurable panels become selectable only
            // while editing. Restoring their original backgrounds prevents
            // invisible viewports from intercepting normal application clicks.
            foreach (KeyValuePair<Controls, Panel> pair in _regionPanels)
            {
                // GuidedHud deliberately stays without a full hit surface:
                // the Classic status indicators are independent regions in
                // the same part of MainCanvas. Its edit marker remains
                // available without swallowing those separate controls.
                if (pair.Key == Controls.GuidedHud)
                    continue;

                pair.Value.Background = enabled
                    ? Brushes.Transparent
                    : _originalPanelBackgrounds[pair.Value];
            }

            foreach (KeyValuePair<TextBlock, Brush?> pair in _originalTextBlockBackgrounds)
            {
                pair.Key.Background = enabled
                    ? Brushes.Transparent
                    : pair.Value;
            }

            if (enabled)
            {
                _overlay.Visibility = Visibility.Visible;
                _guidedHudEditSurface.Visibility = Visibility.Visible;
                _statusText.Visibility = Visibility.Visible;
                _statusText.Text =
                    "LAYOUT EDIT  |  Select a region to move or resize  |  Shift: free corner resize  |  Esc: exit";
                _canvas.Focusable = true;
                _canvas.Focus();
            }
            else
            {
                SetSelectedControl(null);
                _guidedHudEditSurface.Visibility = Visibility.Collapsed;
                _overlay.Visibility = Visibility.Collapsed;
                _outline.Visibility = Visibility.Collapsed;
                foreach (Rectangle handle in _handles.Values)
                    handle.Visibility = Visibility.Collapsed;
                _canvas.Cursor = Cursors.Arrow;
                _canvas.Focusable = _originalCanvasFocusable;
            }
        }

        /// <summary>
        /// Repositions the selection outline after profile switching or when
        /// a configuration is modified through the terminal/Vim interface.
        /// </summary>
        public void RefreshSelection()
        {
            if (_isEnabled && _selectedControl.HasValue)
                UpdateSelectionOverlay();
        }

        private void Canvas_PreviewMouseLeftButtonDown(
            object sender,
            MouseButtonEventArgs e)
        {
            if (!_isEnabled || e.Handled)
                return;

            DependencyObject? source = e.OriginalSource as DependencyObject;

            if (TryFindHandle(source, out LayoutResizeHandle handle))
            {
                if (_selectedControl.HasValue)
                {
                    BeginDrag(_selectedControl.Value, handle, e.GetPosition(_canvas));
                    e.Handled = true;
                }

                return;
            }

            if (TryFindEditSurface(source, out Controls surfaceControl))
            {
                SetSelectedControl(surfaceControl);
                BeginDrag(surfaceControl, null, e.GetPosition(_canvas));
                UpdateSelectionOverlay();
                e.Handled = true;
                return;
            }

            Controls? clickedControl = FindRegion(source);
            if (!clickedControl.HasValue)
                return;

            SetSelectedControl(clickedControl.Value);
            BeginDrag(clickedControl.Value, null, e.GetPosition(_canvas));
            UpdateSelectionOverlay();
            e.Handled = true;
        }

        private void Canvas_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isEnabled || _dragSession == null ||
                e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            DragSession session = _dragSession;

            // If the active profile changed while a drag was in progress,
            // never write the old profile's geometry into the new profile.
            if (!ReferenceEquals(
                session.StartConfig,
                _shiftManager.GetConfig(session.Control)))
            {
                CancelDrag();
                UpdateSelectionOverlay();
                e.Handled = true;
                return;
            }

            Point currentPoint = e.GetPosition(_canvas);
            double deltaX = currentPoint.X - session.StartPoint.X;
            double deltaY = currentPoint.Y - session.StartPoint.Y;

            if (session.Handle.HasValue)
            {
                bool preserveAspectRatio =
                    LayoutManager.IsCorner(session.Handle.Value) &&
                    !Keyboard.IsKeyDown(Key.LeftShift) &&
                    !Keyboard.IsKeyDown(Key.RightShift);

                (double minimumWidth, double minimumHeight) =
                    MinimumSize(session.Control);

                LayoutBounds resized = LayoutManager.Resize(
                    session.StartBounds,
                    session.Handle.Value,
                    deltaX,
                    deltaY,
                    preserveAspectRatio,
                    minimumWidth,
                    minimumHeight,
                    _canvas.Width,
                    _canvas.Height);

                ApplyResize(session, resized);
            }
            else
            {
                LayoutBounds moved = LayoutManager.Move(
                    session.StartBounds,
                    deltaX,
                    deltaY,
                    _canvas.Width,
                    _canvas.Height);

                // Preserve Auto dimensions while normalizing any explicit
                // legacy size that exceeded the canvas bounds.
                double width = double.IsNaN(session.StartConfig.Width)
                    ? double.NaN
                    : moved.Width;
                double height = double.IsNaN(session.StartConfig.Height)
                    ? double.NaN
                    : moved.Height;

                _shiftManager.SetLayout(
                    session.Control,
                    moved.Left,
                    moved.Top,
                    width,
                    height);
            }

            UpdateSelectionOverlay();
            e.Handled = true;
        }

        private void Canvas_PreviewMouseLeftButtonUp(
            object sender,
            MouseButtonEventArgs e)
        {
            if (!_isEnabled)
                return;

            if (_dragSession != null)
            {
                CancelDrag();
                UpdateSelectionOverlay();
                e.Handled = true;
            }
        }

        private void Canvas_PreviewMouseRightButtonDown(
            object sender,
            MouseButtonEventArgs e)
        {
            if (_isEnabled && FindRegion(e.OriginalSource as DependencyObject).HasValue)
                e.Handled = true;
        }

        private void Canvas_PreviewMouseRightButtonUp(
            object sender,
            MouseButtonEventArgs e)
        {
            if (_isEnabled && FindRegion(e.OriginalSource as DependencyObject).HasValue)
                e.Handled = true;
        }

        private void BeginDrag(
            Controls control,
            LayoutResizeHandle? handle,
            Point startPoint)
        {
            Config config = _shiftManager.GetConfig(control);
            LayoutBounds bounds = GetCurrentBounds(control, config);

            _dragSession = new DragSession
            {
                Control = control,
                Handle = handle,
                StartPoint = startPoint,
                StartBounds = bounds,
                StartConfig = config,
                StartFontSize = config.FontSize,
                StartContentScale = config.ContentScale
            };

            _canvas.Cursor = handle.HasValue
                ? CursorFor(handle.Value)
                : Cursors.SizeAll;

            Mouse.Capture(_canvas, CaptureMode.Element);
        }

        private void ApplyResize(DragSession session, LayoutBounds bounds)
        {
            double widthScale = bounds.Width / Math.Max(1, session.StartBounds.Width);
            double heightScale = bounds.Height / Math.Max(1, session.StartBounds.Height);
            double contentScale = Math.Sqrt(widthScale * heightScale);
            contentScale = Math.Clamp(
                contentScale,
                FontScaleMinimum,
                FontScaleMaximum);

            if (session.Control is Controls.TitleBox
                or Controls.TranslationBox
                or Controls.FrequencyBox
                or Controls.AliasBox
                or Controls.ModeBox)
            {
                double fontSize = Math.Clamp(
                    session.StartFontSize * contentScale,
                    6,
                    240);

                _shiftManager.SetLayout(
                    session.Control,
                    bounds.Left,
                    bounds.Top,
                    bounds.Width,
                    bounds.Height,
                    fontSize: fontSize);
            }
            else if (session.Control is Controls.RightList or Controls.BottomList)
            {
                double groupScale = Math.Clamp(
                    session.StartContentScale * contentScale,
                    0.35,
                    4.0);

                _shiftManager.SetLayout(
                    session.Control,
                    bounds.Left,
                    bounds.Top,
                    bounds.Width,
                    bounds.Height,
                    contentScale: groupScale);
            }
            else
            {
                _shiftManager.SetLayout(
                    session.Control,
                    bounds.Left,
                    bounds.Top,
                    bounds.Width,
                    bounds.Height);
            }
        }

        private LayoutBounds GetCurrentBounds(Controls control, Config config)
        {
            FrameworkElement element = _regions[control];

            double width = double.IsNaN(config.Width)
                ? ActualDimension(element.ActualWidth, element.DesiredSize.Width)
                : config.Width;

            double height = double.IsNaN(config.Height)
                ? ActualDimension(element.ActualHeight, element.DesiredSize.Height)
                : config.Height;

            // A collapsed TextBlock has no ActualSize or DesiredSize. Keep a
            // usable edit rectangle for it rather than reducing its frame to
            // a one-pixel line. Visible Auto-sized text uses its real size.
            if (element is TextBlock && width <= 1)
                width = Math.Max(MinimumSize(control).Width, config.FontSize * 8);

            if (element is TextBlock && height <= 1)
                height = Math.Max(MinimumSize(control).Height, config.FontSize * 1.5);

            (double minimumWidth, double minimumHeight) = MinimumSize(control);

            return LayoutManager.Constrain(
                new LayoutBounds(
                    config.Left,
                    config.Top,
                    Math.Max(1, width),
                    Math.Max(1, height)),
                minimumWidth,
                minimumHeight,
                _canvas.Width,
                _canvas.Height);
        }

        private static double ActualDimension(double actual, double desired)
        {
            if (double.IsFinite(actual) && actual > 0)
                return actual;

            if (double.IsFinite(desired) && desired > 0)
                return desired;

            return 1;
        }

        private static (double Width, double Height) MinimumSize(Controls control)
        {
            return control switch
            {
                Controls.TitleBox => (120, 30),
                Controls.TranslationBox => (90, 26),
                Controls.FrequencyBox => (90, 24),
                Controls.AliasBox => (160, 24),
                Controls.ModeBox => (80, 28),
                Controls.VerseBox => (240, 120),
                Controls.GuidedHud => (120, 36),
                Controls.RightList => (85, 180),
                Controls.BottomList => (160, 42),
                _ => (40, 30)
            };
        }

        private void UpdateSelectionOverlay()
        {
            if (!_isEnabled || !_selectedControl.HasValue ||
                !_regions.ContainsKey(_selectedControl.Value))
            {
                _outline.Visibility = Visibility.Collapsed;
                foreach (Rectangle handle in _handles.Values)
                    handle.Visibility = Visibility.Collapsed;
                return;
            }

            Controls control = _selectedControl.Value;
            Config config = _shiftManager.GetConfig(control);
            ObserveConfig(config);
            LayoutBounds bounds = GetCurrentBounds(control, config);

            _outline.Visibility = Visibility.Visible;
            _outline.Width = bounds.Width;
            _outline.Height = bounds.Height;
            Canvas.SetLeft(_outline, bounds.Left);
            Canvas.SetTop(_outline, bounds.Top);

            PlaceHandle(LayoutResizeHandle.NorthWest, bounds.Left, bounds.Top);
            PlaceHandle(LayoutResizeHandle.North, bounds.Left + bounds.Width / 2, bounds.Top);
            PlaceHandle(LayoutResizeHandle.NorthEast, bounds.Right, bounds.Top);
            PlaceHandle(LayoutResizeHandle.East, bounds.Right, bounds.Top + bounds.Height / 2);
            PlaceHandle(LayoutResizeHandle.SouthEast, bounds.Right, bounds.Bottom);
            PlaceHandle(LayoutResizeHandle.South, bounds.Left + bounds.Width / 2, bounds.Bottom);
            PlaceHandle(LayoutResizeHandle.SouthWest, bounds.Left, bounds.Bottom);
            PlaceHandle(LayoutResizeHandle.West, bounds.Left, bounds.Top + bounds.Height / 2);

            _statusText.Text =
                $"LAYOUT EDIT  |  {control}  |  Drag body: move  |  Corner: proportional  |  Shift: free resize  |  Esc: exit";
        }

        private void PlaceHandle(
            LayoutResizeHandle handle,
            double centreX,
            double centreY)
        {
            if (!_handles.TryGetValue(handle, out Rectangle? element))
                return;

            element.Visibility = Visibility.Visible;
            Canvas.SetLeft(element, centreX - HandleSize / 2);
            Canvas.SetTop(element, centreY - HandleSize / 2);
        }

        private void SetSelectedControl(Controls? control)
        {
            if (_selectedControl == control)
                return;

            if (_observedConfig != null)
                _observedConfig.PropertyChanged -= SelectedConfig_PropertyChanged;

            _observedConfig = null;
            _selectedControl = control;

            if (control.HasValue)
                ObserveConfig(_shiftManager.GetConfig(control.Value));
        }

        private void ObserveConfig(Config config)
        {
            if (ReferenceEquals(_observedConfig, config))
                return;

            if (_observedConfig != null)
                _observedConfig.PropertyChanged -= SelectedConfig_PropertyChanged;

            _observedConfig = config;
            _observedConfig.PropertyChanged += SelectedConfig_PropertyChanged;
        }

        private void SelectedConfig_PropertyChanged(
            object? sender,
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_isEnabled && _selectedControl.HasValue)
                UpdateSelectionOverlay();
        }

        private void RegisteredRegion_SizeChanged(
            object sender,
            SizeChangedEventArgs e)
        {
            if (_isEnabled && _selectedControl.HasValue &&
                ReferenceEquals(_regions[_selectedControl.Value], sender))
            {
                UpdateSelectionOverlay();
            }
        }

        private bool TryFindHandle(
            DependencyObject? source,
            out LayoutResizeHandle handle)
        {
            DependencyObject? current = source;

            while (current != null && !ReferenceEquals(current, _canvas))
            {
                if (current is FrameworkElement element &&
                    element.Tag is string tag &&
                    Enum.TryParse(tag, true, out handle) &&
                    _handles.ContainsKey(handle))
                {
                    return true;
                }

                current = GetParent(current);
            }

            handle = default;
            return false;
        }

        private bool TryFindEditSurface(
            DependencyObject? source,
            out Controls control)
        {
            DependencyObject? current = source;
            while (current != null && !ReferenceEquals(current, _canvas))
            {
                if (current is FrameworkElement element &&
                    _editSurfaces.TryGetValue(element, out control))
                {
                    return true;
                }

                current = GetParent(current);
            }

            control = default;
            return false;
        }

        private Controls? FindRegion(DependencyObject? source)
        {
            if (source == null)
                return null;

            // Registration order is not relied upon: select the deepest
            // matching registered element in the visual/logical ancestry.
            DependencyObject? current = source;
            while (current != null && !ReferenceEquals(current, _canvas))
            {
                foreach (KeyValuePair<Controls, FrameworkElement> pair in _regions)
                {
                    if (ReferenceEquals(current, pair.Value))
                        return pair.Key;
                }

                current = GetParent(current);
            }

            return null;
        }

        private static DependencyObject? GetParent(DependencyObject element)
        {
            if (element is Visual or Visual3D)
            {
                DependencyObject? visualParent = VisualTreeHelper.GetParent(element);
                if (visualParent != null)
                    return visualParent;
            }

            return LogicalTreeHelper.GetParent(element);
        }

        private static Cursor CursorFor(LayoutResizeHandle handle)
        {
            return handle switch
            {
                LayoutResizeHandle.NorthWest or LayoutResizeHandle.SouthEast => Cursors.SizeNWSE,
                LayoutResizeHandle.NorthEast or LayoutResizeHandle.SouthWest => Cursors.SizeNESW,
                LayoutResizeHandle.North or LayoutResizeHandle.South => Cursors.SizeNS,
                LayoutResizeHandle.East or LayoutResizeHandle.West => Cursors.SizeWE,
                _ => Cursors.Arrow
            };
        }

        private void CancelDrag()
        {
            _dragSession = null;
            if (Mouse.Captured == _canvas)
                Mouse.Capture(null);
            _canvas.Cursor = Cursors.Arrow;
        }

        private sealed class DragSession
        {
            public required Controls Control { get; init; }
            public required LayoutResizeHandle? Handle { get; init; }
            public required Point StartPoint { get; init; }
            public required LayoutBounds StartBounds { get; init; }
            public required Config StartConfig { get; init; }
            public required double StartFontSize { get; init; }
            public required double StartContentScale { get; init; }
        }
    }
}
