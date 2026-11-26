using System;

namespace navigationshurely.services
{
    /// <summary>
    /// The eight resize grips surrounding a selectable layout region.
    /// </summary>
    public enum LayoutResizeHandle
    {
        NorthWest,
        North,
        NorthEast,
        East,
        SouthEast,
        South,
        SouthWest,
        West
    }

    /// <summary>
    /// A rectangle expressed in MainCanvas logical coordinates.
    /// This type deliberately contains no WPF controls or application state.
    /// </summary>
    public readonly record struct LayoutBounds(
        double Left,
        double Top,
        double Width,
        double Height)
    {
        public double Right => Left + Width;
        public double Bottom => Top + Height;
    }

    /// <summary>
    /// Performs the geometry calculations used by LayoutEditor.
    /// It does not own or persist layout state; Config remains authoritative.
    /// </summary>
    public static class LayoutManager
    {
        public static LayoutBounds Constrain(
            LayoutBounds bounds,
            double minimumWidth,
            double minimumHeight,
            double canvasWidth,
            double canvasHeight)
        {
            ValidateCanvas(canvasWidth, canvasHeight);

            minimumWidth = Math.Clamp(minimumWidth, 1, canvasWidth);
            minimumHeight = Math.Clamp(minimumHeight, 1, canvasHeight);

            double width = double.IsFinite(bounds.Width)
                ? Math.Clamp(bounds.Width, minimumWidth, canvasWidth)
                : minimumWidth;

            double height = double.IsFinite(bounds.Height)
                ? Math.Clamp(bounds.Height, minimumHeight, canvasHeight)
                : minimumHeight;

            double left = double.IsFinite(bounds.Left)
                ? Math.Clamp(bounds.Left, 0, canvasWidth - width)
                : 0;

            double top = double.IsFinite(bounds.Top)
                ? Math.Clamp(bounds.Top, 0, canvasHeight - height)
                : 0;

            return new LayoutBounds(left, top, width, height);
        }

        public static LayoutBounds Move(
            LayoutBounds original,
            double deltaX,
            double deltaY,
            double canvasWidth,
            double canvasHeight)
        {
            LayoutBounds constrained = Constrain(
                original,
                1,
                1,
                canvasWidth,
                canvasHeight);

            double left = Math.Clamp(
                constrained.Left + deltaX,
                0,
                canvasWidth - constrained.Width);

            double top = Math.Clamp(
                constrained.Top + deltaY,
                0,
                canvasHeight - constrained.Height);

            return new LayoutBounds(
                left,
                top,
                constrained.Width,
                constrained.Height);
        }

        public static LayoutBounds Resize(
            LayoutBounds original,
            LayoutResizeHandle handle,
            double deltaX,
            double deltaY,
            bool preserveAspectRatio,
            double minimumWidth,
            double minimumHeight,
            double canvasWidth,
            double canvasHeight)
        {
            original = Constrain(
                original,
                minimumWidth,
                minimumHeight,
                canvasWidth,
                canvasHeight);

            minimumWidth = Math.Clamp(minimumWidth, 1, canvasWidth);
            minimumHeight = Math.Clamp(minimumHeight, 1, canvasHeight);

            bool west = handle is LayoutResizeHandle.NorthWest
                or LayoutResizeHandle.West
                or LayoutResizeHandle.SouthWest;

            bool east = handle is LayoutResizeHandle.NorthEast
                or LayoutResizeHandle.East
                or LayoutResizeHandle.SouthEast;

            bool north = handle is LayoutResizeHandle.NorthWest
                or LayoutResizeHandle.North
                or LayoutResizeHandle.NorthEast;

            bool south = handle is LayoutResizeHandle.SouthWest
                or LayoutResizeHandle.South
                or LayoutResizeHandle.SouthEast;

            bool corner = (west || east) && (north || south);

            double left = original.Left;
            double top = original.Top;
            double right = original.Right;
            double bottom = original.Bottom;

            if (corner && preserveAspectRatio)
            {
                // Project the requested width/height change onto the original
                // aspect-ratio vector. The opposite corner remains anchored.
                double requestedWidth = original.Width + (east ? deltaX : -deltaX);
                double requestedHeight = original.Height + (south ? deltaY : -deltaY);

                double widthRatio = requestedWidth / original.Width;
                double heightRatio = requestedHeight / original.Height;

                double scale =
                    (widthRatio * original.Width * original.Width +
                     heightRatio * original.Height * original.Height) /
                    (original.Width * original.Width +
                     original.Height * original.Height);

                double anchorX = west ? original.Right : original.Left;
                double anchorY = north ? original.Bottom : original.Top;

                double maximumWidth = west
                    ? anchorX
                    : canvasWidth - anchorX;

                double maximumHeight = north
                    ? anchorY
                    : canvasHeight - anchorY;

                double minimumScale = Math.Max(
                    minimumWidth / original.Width,
                    minimumHeight / original.Height);

                double maximumScale = Math.Min(
                    maximumWidth / original.Width,
                    maximumHeight / original.Height);

                // A rectangle touching two canvas edges can produce a tiny
                // floating-point inversion (for example 0.9999999999999999
                // instead of 1). Treat that as an exact minimum-size fit.
                maximumScale = Math.Max(minimumScale, maximumScale);
                scale = Math.Clamp(scale, minimumScale, maximumScale);

                double width = original.Width * scale;
                double height = original.Height * scale;

                left = west ? anchorX - width : anchorX;
                top = north ? anchorY - height : anchorY;

                return new LayoutBounds(left, top, width, height);
            }

            // Unrestricted corner resizing, or an edge grip. The untouched
            // edges remain fixed while each changed dimension is constrained.
            if (west)
            {
                left = Math.Clamp(
                    original.Left + deltaX,
                    0,
                    right - minimumWidth);
            }
            else if (east)
            {
                right = Math.Clamp(
                    original.Right + deltaX,
                    left + minimumWidth,
                    canvasWidth);
            }

            if (north)
            {
                top = Math.Clamp(
                    original.Top + deltaY,
                    0,
                    bottom - minimumHeight);
            }
            else if (south)
            {
                bottom = Math.Clamp(
                    original.Bottom + deltaY,
                    top + minimumHeight,
                    canvasHeight);
            }

            return new LayoutBounds(
                left,
                top,
                right - left,
                bottom - top);
        }

        public static bool IsCorner(LayoutResizeHandle handle)
        {
            return handle is LayoutResizeHandle.NorthWest
                or LayoutResizeHandle.NorthEast
                or LayoutResizeHandle.SouthEast
                or LayoutResizeHandle.SouthWest;
        }

        private static void ValidateCanvas(
            double canvasWidth,
            double canvasHeight)
        {
            if (!double.IsFinite(canvasWidth) || canvasWidth <= 0)
                throw new ArgumentOutOfRangeException(nameof(canvasWidth));

            if (!double.IsFinite(canvasHeight) || canvasHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(canvasHeight));
        }
    }
}
