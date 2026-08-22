using System.Windows;
using WorkStationX.Services;

namespace WorkStationX.Tests;

public class AnnotationTests
{
    private static Annotation Shape(AnnotationTool tool, Point a, Point b) =>
        new() { Tool = tool, Points = { a, b } };

    [Fact]
    public void DraggingUpLeftGivesTheSameRectangleAsDraggingDownRight()
    {
        // Without normalising, a backwards drag yields a negative width and WPF throws.
        var forward = Shape(AnnotationTool.Rectangle, new Point(10, 20), new Point(110, 220));
        var backward = Shape(AnnotationTool.Rectangle, new Point(110, 220), new Point(10, 20));

        Assert.Equal(new Rect(10, 20, 100, 200), forward.Bounds);
        Assert.Equal(forward.Bounds, backward.Bounds);
    }

    [Fact]
    public void BoundsAreNeverNegative()
    {
        var shape = Shape(AnnotationTool.Rectangle, new Point(200, 200), new Point(50, 50));

        Assert.True(shape.Bounds.Width >= 0);
        Assert.True(shape.Bounds.Height >= 0);
    }

    [Fact]
    public void ArrowHeadSitsBehindTheTipNotBeyondIt()
    {
        // A horizontal arrow pointing right: both barbs must be to the LEFT of the tip,
        // or the head renders past the end of the shaft.
        var arrow = Shape(AnnotationTool.Arrow, new Point(0, 100), new Point(200, 100));

        var (left, right) = arrow.ArrowHead();

        Assert.True(left.X < arrow.End.X);
        Assert.True(right.X < arrow.End.X);
    }

    [Fact]
    public void ArrowHeadBarbsStraddleTheShaft()
    {
        var arrow = Shape(AnnotationTool.Arrow, new Point(0, 100), new Point(200, 100));

        var (left, right) = arrow.ArrowHead();

        // One barb each side of the shaft, equally far off it. Which one is which
        // depends on screen coordinates running Y-down, so the test asserts the
        // property that matters rather than a particular side.
        Assert.True(
            (left.Y - 100) * (right.Y - 100) < 0,
            "the two barbs must fall on opposite sides of the shaft");
        Assert.Equal(Math.Abs(left.Y - 100), Math.Abs(right.Y - 100), precision: 6);
    }

    [Fact]
    public void ArrowHeadFollowsTheShaftDirection()
    {
        // Pointing straight down: the barbs must be ABOVE the tip.
        var arrow = Shape(AnnotationTool.Arrow, new Point(100, 0), new Point(100, 200));

        var (left, right) = arrow.ArrowHead();

        Assert.True(left.Y < arrow.End.Y);
        Assert.True(right.Y < arrow.End.Y);
    }

    [Fact]
    public void ArrowHeadLengthIsRespected()
    {
        var arrow = Shape(AnnotationTool.Arrow, new Point(0, 0), new Point(100, 0));

        var (left, _) = arrow.ArrowHead(length: 30);

        var distance = Math.Sqrt(
            Math.Pow(left.X - arrow.End.X, 2) + Math.Pow(left.Y - arrow.End.Y, 2));

        Assert.Equal(30, distance, precision: 6);
    }

    [Fact]
    public void AZeroLengthArrowDoesNotProduceNaN()
    {
        // Atan2(0, 0) is defined, but the result still has to be finite or the
        // renderer throws when it builds the geometry.
        var arrow = Shape(AnnotationTool.Arrow, new Point(50, 50), new Point(50, 50));

        var (left, right) = arrow.ArrowHead();

        Assert.False(double.IsNaN(left.X) || double.IsNaN(left.Y));
        Assert.False(double.IsNaN(right.X) || double.IsNaN(right.Y));
    }

    [Fact]
    public void AnEmptyAnnotationHasSafeDefaults()
    {
        var empty = new Annotation { Tool = AnnotationTool.Pen };

        Assert.Equal(default, empty.Start);
        Assert.Equal(default, empty.End);
        Assert.Equal(0, empty.Bounds.Width);
    }

    [Fact]
    public void PenKeepsEveryPointWhileShapesUseOnlyTheEnds()
    {
        var pen = new Annotation
        {
            Tool = AnnotationTool.Pen,
            Points = { new Point(0, 0), new Point(5, 5), new Point(10, 2) }
        };

        Assert.Equal(3, pen.Points.Count);
        Assert.Equal(new Point(0, 0), pen.Start);
        Assert.Equal(new Point(10, 2), pen.End);
    }
}
