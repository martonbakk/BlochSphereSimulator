using BlochSphereSimulator.Models;
using BlochSphereSimulator.Rendering;
using BlochSphereSimulator.ViewModels;
using HelixToolkit.Wpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace BlochSphereSimulator
{
    public partial class MainWindow : Window
    {
        private static readonly Point3D CameraPosition = new(6.0, 2.2, 2.8);
        private static readonly Vector3D CameraLook = new(-6.0, -2.2, -2.8);

        // World-space diameters of the path tubes (real 3D geometry, see RedrawScene).
        private const double PathDiameterSelected = 0.016;
        private const double PathDiameterOther = 0.010;

        private readonly MainViewModel _vm;

        /// <summary>Everything that depends on the qubit states; rebuilt on each change.</summary>
        private readonly ModelVisual3D _dynamicRoot = new();

        /// <summary>2D labels drawn over the 3D view. Anchors are 3D points, re-projected when the camera moves.</summary>
        private readonly List<OverlayLabel> _staticLabels = new();
        private readonly List<OverlayLabel> _dynamicLabels = new();

        // Animation state
        private QubitStep? _animStep;
        private readonly Stopwatch _animClock = new();
        private double _animDurationMs;
        private ArrowVisual3D? _animArrow;
        private ModelVisual3D? _animTrail;
        private Color _animColor;

        public MainWindow()
        {
            InitializeComponent();

            MainViewport.RotateGesture = new MouseGesture(MouseAction.LeftClick);
            MainViewport.PanGesture = new MouseGesture(MouseAction.RightClick);

            DrawStaticScene();
            MainViewport.Children.Add(_dynamicRoot);

            // Faint translucent sphere surface for depth perception. WPF blends transparent
            // geometry correctly only if it is drawn after the opaque content, so it goes last.
            // Diffuse-only material: a specular one produces a bright glare in the middle of the sphere.
            var shell = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(22, 120, 140, 170)));
            MainViewport.Children.Add(new SphereVisual3D
            {
                Center = new Point3D(0, 0, 0),
                Radius = 0.995,
                ThetaDiv = 48,
                PhiDiv = 24,
                Material = shell,
                BackMaterial = shell
            });

            _vm = new MainViewModel();
            DataContext = _vm;
            _vm.SceneChanged += (_, _) => { StopAnimation(); RedrawScene(); };
            _vm.StepAdded += OnStepAdded;

            MainViewport.CameraChanged += (_, _) => PositionLabels();
            LabelCanvas.SizeChanged += (_, _) => PositionLabels();
            Loaded += (_, _) => PositionLabels();

            RedrawScene();
        }

        // ===================== UI helpers =====================

        /// <summary>Enter in a number box commits the value immediately.</summary>
        private void NumberBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox box)
                box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        }

        private void ResetCamera_Click(object sender, RoutedEventArgs e)
        {
            if (MainViewport.Camera is PerspectiveCamera cam)
            {
                cam.Position = CameraPosition;
                cam.LookDirection = CameraLook;
                cam.UpDirection = new Vector3D(0, 0, 1);
            }
        }

        // ===================== Static scene =====================

        private void DrawStaticScene()
        {
            // Wireframe: created once and never replaced, so the screen-space lines are stable.
            var grid = new Point3DCollection();
            const int segments = 64;
            for (int i = 0; i < 12; i++)
            {
                double az = i * Math.PI / 12;
                for (int j = 0; j < segments; j++)
                {
                    double p1 = j * 2 * Math.PI / segments, p2 = (j + 1) * 2 * Math.PI / segments;
                    grid.Add(new Point3D(Math.Sin(p1) * Math.Cos(az), Math.Sin(p1) * Math.Sin(az), Math.Cos(p1)));
                    grid.Add(new Point3D(Math.Sin(p2) * Math.Cos(az), Math.Sin(p2) * Math.Sin(az), Math.Cos(p2)));
                }
            }
            for (int i = 1; i < 12; i++)
            {
                if (i == 6) continue; // the equator is drawn separately
                double polar = i * Math.PI / 12;
                AddCircle(grid, Math.Sin(polar), Math.Cos(polar), segments);
            }
            MainViewport.Children.Add(new LinesVisual3D { Points = grid, Color = Color.FromRgb(215, 215, 215), Thickness = 0.8 });

            var equator = new Point3DCollection();
            AddCircle(equator, 1, 0, 96);
            MainViewport.Children.Add(new LinesVisual3D { Points = equator, Color = Color.FromRgb(150, 150, 150), Thickness = 1.6 });

            var red = Color.FromRgb(235, 87, 87);
            var green = Color.FromRgb(39, 174, 96);
            var blue = Color.FromRgb(64, 140, 235);
            MainViewport.Children.Add(new ArrowVisual3D { Point1 = new Point3D(-1.3, 0, 0), Point2 = new Point3D(1.3, 0, 0), Diameter = 0.015, Fill = new SolidColorBrush(red) });
            MainViewport.Children.Add(new ArrowVisual3D { Point1 = new Point3D(0, -1.3, 0), Point2 = new Point3D(0, 1.3, 0), Diameter = 0.015, Fill = new SolidColorBrush(green) });
            MainViewport.Children.Add(new ArrowVisual3D { Point1 = new Point3D(0, 0, -1.3), Point2 = new Point3D(0, 0, 1.3), Diameter = 0.015, Fill = new SolidColorBrush(blue) });

            // Anchored at the ends of the axis lines (±1.3), placed just beyond them on screen.
            AddStaticLabel("x  |+⟩", new Point3D(1.3, 0, 0), red, 17);
            AddStaticLabel("|−⟩", new Point3D(-1.3, 0, 0), red, 15);
            AddStaticLabel("y  |+j⟩", new Point3D(0, 1.3, 0), green, 17);
            AddStaticLabel("|−j⟩", new Point3D(0, -1.3, 0), green, 15);
            AddStaticLabel("z  |0⟩", new Point3D(0, 0, 1.3), blue, 17);
            AddStaticLabel("|1⟩", new Point3D(0, 0, -1.3), blue, 15);
        }

        private static void AddCircle(Point3DCollection pts, double r, double z, int segments)
        {
            for (int j = 0; j < segments; j++)
            {
                double a1 = j * 2 * Math.PI / segments, a2 = (j + 1) * 2 * Math.PI / segments;
                pts.Add(new Point3D(r * Math.Cos(a1), r * Math.Sin(a1), z));
                pts.Add(new Point3D(r * Math.Cos(a2), r * Math.Sin(a2), z));
            }
        }

        private void AddStaticLabel(string text, Point3D axisEnd, Color color, double size)
        {
            // 0.03: about the radius of the axis arrow head
            var label = new OverlayLabel(axisEnd, 0.03, CreateLabelBlock(text, color, size));
            _staticLabels.Add(label);
            LabelCanvas.Children.Add(label.Block);
        }

        // ===================== Dynamic scene =====================

        private void RedrawScene() => RedrawScene(null, null);

        /// <summary>
        /// Draws every qubit. If <paramref name="animTrack"/> is set, that track is drawn as it was
        /// before <paramref name="animStep"/> (the animation then moves the arrow to the new state).
        /// </summary>
        private void RedrawScene(QubitTrack? animTrack, QubitStep? animStep)
        {
            _dynamicRoot.Children.Clear();
            ClearDynamicLabels();

            foreach (var track in _vm.Qubits)
            {
                bool selected = track == _vm.SelectedQubit;
                var steps = track.Steps.ToList();
                // While a step animates, the moving arrow is this qubit's current vector, so the state it
                // starts from is drawn as an ordinary (faint) history arrow instead of a second main arrow.
                bool animating = track == animTrack && animStep != null && steps.LastOrDefault() == animStep;
                if (animating) steps.RemoveAt(steps.Count - 1);
                if (steps.Count == 0) continue;
                int last = steps.Count - 1;

                if (_vm.ShowPaths && last > 0)
                {
                    // All paths of one qubit go into one mesh: one visual, no per-frame screen-space work.
                    var builder = new TubeMeshBuilder();
                    for (int i = 1; i <= last; i++)
                        AddPathGeometry(builder, steps[i].Path, steps[i].Kind == StepKind.Measurement,
                                        selected ? PathDiameterSelected : PathDiameterOther);
                    var color = selected ? track.Color : Color.FromArgb(150, track.Color.R, track.Color.G, track.Color.B);
                    AddMesh(builder, color);
                }

                int historyEnd = animating ? last + 1 : last; // exclusive
                if (_vm.ShowHistory || animating)
                {
                    for (int i = 0; i < historyEnd; i++)
                    {
                        if (!_vm.ShowHistory && i < last) continue; // history hidden: only the animation's start state
                        // Older steps are fainter; the order is shown by the numbers.
                        double t = historyEnd <= 1 ? 1 : (double)i / (historyEnd - 1);
                        byte alpha = (byte)((selected ? 70 : 45) + t * (selected ? 90 : 50));
                        AddArrow(steps[i].Bloch, Color.FromArgb(alpha, track.Color.R, track.Color.G, track.Color.B), selected ? 0.018 : 0.012, 0.08);
                    }
                }

                if (!animating)
                {
                    AddArrow(steps[last].Bloch, track.Color, selected ? 0.045 : 0.03, selected ? 0.18 : 0.14);
                    _dynamicRoot.Children.Add(new SphereVisual3D { Center = steps[last].Bloch, Radius = selected ? 0.04 : 0.03, Fill = track.Brush });
                }

                AddTipLabels(track, steps, selected);
            }

            PositionLabels();
        }

        /// <summary>
        /// Labels at the arrow tips: step numbers for the selected qubit (steps that end on the same
        /// point share one label, e.g. "0, 2") and the qubit name when there is more than one qubit.
        /// </summary>
        private void AddTipLabels(QubitTrack track, List<QubitStep> steps, bool selected)
        {
            bool showName = _vm.Qubits.Count > 1;
            int last = steps.Count - 1;

            if (selected && _vm.ShowHistory && last > 0)
            {
                var groups = Enumerable.Range(0, steps.Count)
                    .GroupBy(i => (Math.Round(steps[i].Bloch.X, 3), Math.Round(steps[i].Bloch.Y, 3), Math.Round(steps[i].Bloch.Z, 3)));
                foreach (var g in groups)
                {
                    var indices = g.ToList();
                    string text = string.Join(", ", indices.Select(i => i.ToString(CultureInfo.InvariantCulture)));
                    if (showName && indices.Contains(last)) text = $"{track.Name}: {text}";
                    AddDynamicLabel(text, steps[indices[0]].Bloch, track.Color, 13);
                }
            }
            else if (showName)
            {
                AddDynamicLabel(track.Name, steps[last].Bloch, track.Color, 13);
            }
        }

        private void AddArrow(Point3D tip, Color color, double diameter, double headLength)
        {
            if (Length(tip) < 1e-6) return;
            _dynamicRoot.Children.Add(new ArrowVisual3D
            {
                Point1 = new Point3D(0, 0, 0),
                Point2 = tip,
                Diameter = diameter,
                HeadLength = headLength / diameter, // Helix expects the head length relative to the diameter
                Fill = new SolidColorBrush(color)
            });
        }

        /// <summary>
        /// Adds a path as cylinder segments: solid for gates, dashed for measurement "jumps".
        /// Segments shorter than a few thousandths are skipped (e.g. a gate whose axis passes through the state).
        /// </summary>
        private static void AddPathGeometry(TubeMeshBuilder builder, IReadOnlyList<Point3D> path, bool dashed, double diameter)
        {
            for (int i = 0; i < path.Count - 1; i++)
            {
                if (dashed && i % 2 == 1) continue;
                if ((path[i + 1] - path[i]).Length < 1e-4) continue;
                // Extend each segment slightly so neighbouring cylinders overlap and the arc has no gaps
                var d = path[i + 1] - path[i];
                d.Normalize();
                builder.AddCylinder(path[i] - d * diameter * 0.25, path[i + 1] + d * diameter * 0.25, diameter, 8);
            }
        }

        private void AddMesh(TubeMeshBuilder builder, Color color)
        {
            var model = builder.ToModel(color);
            if (model != null) _dynamicRoot.Children.Add(new ModelVisual3D { Content = model });
        }

        // ===================== Labels (2D overlay) =====================

        private static TextBlock CreateLabelBlock(string text, Color color, double size) => new()
        {
            Text = text,
            Foreground = new SolidColorBrush(color),
            FontSize = size,
            FontWeight = FontWeights.Bold,
            // No background box; a soft white glow keeps the text readable over the grid lines.
            Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.White, BlurRadius = 6, ShadowDepth = 0, Opacity = 1 }
        };

        private void AddDynamicLabel(string text, Point3D tip, Color color, double size)
        {
            // 0.045: radius of the sphere drawn on the current state's tip (history tips are thinner)
            var label = new OverlayLabel(tip, 0.045, CreateLabelBlock(text, color, size));
            _dynamicLabels.Add(label);
            LabelCanvas.Children.Add(label.Block);
        }

        private void ClearDynamicLabels()
        {
            foreach (var l in _dynamicLabels) LabelCanvas.Children.Remove(l.Block);
            _dynamicLabels.Clear();
        }

        /// <summary>
        /// Projects every label anchor (an arrow or axis end) onto the screen and places the label just
        /// beyond it, in the screen direction in which the arrow points:
        ///  - gap = on-screen radius of the tip (sphere / arrow head) + a few pixels, so it stays tight at any zoom;
        ///  - the label is pushed out by its own half-extent along that direction, so the nearest edge of the
        ///    text (not its centre) sits at the gap - for any direction, including diagonals.
        /// </summary>
        private void PositionLabels()
        {
            var viewport = MainViewport.Viewport;
            if (viewport?.Camera is not ProjectionCamera camera || LabelCanvas.ActualWidth <= 0) return;

            // A world-space vector perpendicular to the view direction, to measure on-screen sizes
            var look = camera.LookDirection;
            look.Normalize();
            var screenUp = camera.UpDirection - look * Vector3D.DotProduct(camera.UpDirection, look);
            if (screenUp.Length < 1e-9) screenUp = new Vector3D(0, 0, 1);
            screenUp.Normalize();

            foreach (var label in _staticLabels.Concat(_dynamicLabels))
            {
                var block = label.Block;
                var p = Viewport3DHelper.Point3DtoPoint2D(viewport, label.Anchor);
                if (!IsFinite(p)) { block.Visibility = Visibility.Hidden; continue; }

                block.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double w = block.DesiredSize.Width, h = block.DesiredSize.Height;

                // Direction of the arrow on screen at its end
                var q = Viewport3DHelper.Point3DtoPoint2D(viewport, Scale(label.Anchor, 1.2));
                var dir = IsFinite(q) ? new Vector(q.X - p.X, q.Y - p.Y) : new Vector();
                if (dir.Length < 3) dir = new Vector(0.7071, -0.7071); // arrow points at the viewer: up-right
                dir.Normalize();

                // A state on a coordinate axis (|0⟩, |1⟩, |±⟩, |±j⟩) has the axis line running on beyond its tip,
                // so a label straight ahead would sit on that line. Put it beside the tip instead
                // (perpendicular on screen, preferring the upper / right side).
                if (label.TipRadius > 0.04 && OnCoordinateAxis(label.Anchor))
                {
                    var side = new Vector(-dir.Y, dir.X);
                    if (side.Y > 0.2 || (Math.Abs(side.Y) <= 0.2 && side.X < 0)) side = -side;
                    dir = side;
                }

                // On-screen radius of the tip
                var edge = Viewport3DHelper.Point3DtoPoint2D(viewport, label.Anchor + screenUp * label.TipRadius);
                double tipRadius = IsFinite(edge) ? (edge - p).Length : 0;
                double gap = tipRadius + 4;

                // Distance from the anchor to the label centre so that the rectangle's nearest point is `gap` away:
                // half-extent of a w x h rectangle along dir is |dx|*w/2 + |dy|*h/2.
                double halfExtent = Math.Abs(dir.X) * w / 2 + Math.Abs(dir.Y) * h / 2;
                double cx = p.X + dir.X * (gap + halfExtent);
                double cy = p.Y + dir.Y * (gap + halfExtent);

                Canvas.SetLeft(block, Math.Round(cx - w / 2));
                Canvas.SetTop(block, Math.Round(cy - h / 2));
                block.Visibility = Visibility.Visible;
            }
        }

        private static bool IsFinite(Point p) => double.IsFinite(p.X) && double.IsFinite(p.Y);

        private static bool OnCoordinateAxis(Point3D p) =>
            Math.Abs(Math.Abs(p.X) - 1) < 1e-6 || Math.Abs(Math.Abs(p.Y) - 1) < 1e-6 || Math.Abs(Math.Abs(p.Z) - 1) < 1e-6;

        // ===================== Animation =====================

        private void OnStepAdded(object? sender, StepAddedEventArgs e)
        {
            StopAnimation();
            if (!_vm.AnimationEnabled || e.Step.Path.Count < 2)
            {
                RedrawScene();
                return;
            }

            _animStep = e.Step;
            _animColor = e.Track.Color;
            _animDurationMs = _vm.AnimationSeconds * 1000 * (e.Step.Kind == StepKind.Measurement ? 0.6 : 1.0);
            RedrawScene(e.Track, e.Step);

            // Show the rotation axis of the gate while it acts (dashed, as real 3D geometry)
            if (e.Step.Kind == StepKind.Gate && e.Step.Axis.HasValue)
            {
                var a = e.Step.Axis.Value * 1.35;
                var axisPath = QuantumMath.StraightPath(new Point3D(-a.X, -a.Y, -a.Z), new Point3D(a.X, a.Y, a.Z), 30);
                var builder = new TubeMeshBuilder();
                AddPathGeometry(builder, axisPath, true, 0.012);
                AddMesh(builder, Color.FromRgb(90, 90, 90));
            }

            _animTrail = new ModelVisual3D();
            _animArrow = new ArrowVisual3D { Point1 = new Point3D(0, 0, 0), Point2 = e.Step.Path[0], Diameter = 0.045, HeadLength = 0.18 / 0.045, Fill = e.Track.Brush };
            _dynamicRoot.Children.Add(_animTrail);
            _dynamicRoot.Children.Add(_animArrow);

            _animClock.Restart();
            CompositionTarget.Rendering += OnAnimationFrame;
        }

        private void OnAnimationFrame(object? sender, EventArgs e)
        {
            if (_animStep == null || _animArrow == null || _animTrail == null) return;

            double t = Math.Min(1.0, _animClock.Elapsed.TotalMilliseconds / _animDurationMs);
            double eased = 0.5 - 0.5 * Math.Cos(Math.PI * t); // ease in-out
            var path = _animStep.Path;
            double pos = eased * (path.Count - 1);
            int idx = Math.Clamp((int)Math.Floor(pos), 0, path.Count - 2);
            double frac = pos - idx;
            var a = path[idx];
            var b = path[idx + 1];
            var tip = new Point3D(a.X + (b.X - a.X) * frac, a.Y + (b.Y - a.Y) * frac, a.Z + (b.Z - a.Z) * frac);

            if (Length(tip) > 1e-3) _animArrow.Point2 = tip;

            if (_vm.ShowPaths)
            {
                var partial = path.Take(idx + 1).Append(tip).ToList();
                var builder = new TubeMeshBuilder();
                AddPathGeometry(builder, partial, _animStep.Kind == StepKind.Measurement, PathDiameterSelected);
                _animTrail.Content = builder.ToModel(_animColor);
            }

            if (t >= 1.0)
            {
                StopAnimation();
                RedrawScene();
            }
        }

        private void StopAnimation()
        {
            CompositionTarget.Rendering -= OnAnimationFrame;
            _animClock.Stop();
            _animStep = null;
            _animArrow = null;
            _animTrail = null;
        }

        private static double Length(Point3D p) => Math.Sqrt(p.X * p.X + p.Y * p.Y + p.Z * p.Z);
        private static Point3D Scale(Point3D p, double s) => new(p.X * s, p.Y * s, p.Z * s);

        /// <param name="Anchor">3D end point of the arrow or axis the label belongs to.</param>
        /// <param name="TipRadius">World-space radius of what is drawn at the end (tip sphere / arrow head).</param>
        private sealed record OverlayLabel(Point3D Anchor, double TipRadius, TextBlock Block);
    }
}
