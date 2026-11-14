using BlochSphereSimulator.Models;
using BlochSphereSimulator.ViewModels;
using HelixToolkit.Wpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace BlochSphereSimulator
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;

        /// <summary>Everything that depends on the qubit states; rebuilt on each change.</summary>
        private readonly ModelVisual3D _dynamicRoot = new();

        // Animation state
        private QubitTrack? _animTrack;
        private QubitStep? _animStep;
        private readonly Stopwatch _animClock = new();
        private double _animDurationMs;
        private ArrowVisual3D? _animArrow;
        private LinesVisual3D? _animPathLine;

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
                cam.Position = new Point3D(6.0, 2.2, 2.8);
                cam.LookDirection = new Vector3D(-6.0, -2.2, -2.8);
                cam.UpDirection = new Vector3D(0, 0, 1);
            }
        }

        // ===================== Static scene =====================

        private void DrawStaticScene()
        {
            var grid = new Point3DCollection();
            const int segments = 64;

            // Meridians
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
            // Parallels (without the equator, drawn separately)
            for (int i = 1; i < 12; i++)
            {
                if (i == 6) continue;
                double polar = i * Math.PI / 12, z = Math.Cos(polar), r = Math.Sin(polar);
                AddCircle(grid, r, z, segments);
            }
            MainViewport.Children.Add(new LinesVisual3D { Points = grid, Color = Color.FromRgb(215, 215, 215), Thickness = 0.8 });

            var equator = new Point3DCollection();
            AddCircle(equator, 1, 0, 96);
            MainViewport.Children.Add(new LinesVisual3D { Points = equator, Color = Color.FromRgb(150, 150, 150), Thickness = 1.6 });

            var red = new SolidColorBrush(Color.FromRgb(255, 107, 107));
            var green = new SolidColorBrush(Color.FromRgb(46, 204, 113));
            var blue = new SolidColorBrush(Color.FromRgb(84, 160, 255));
            MainViewport.Children.Add(new ArrowVisual3D { Point1 = new Point3D(-1.3, 0, 0), Point2 = new Point3D(1.3, 0, 0), Diameter = 0.015, Fill = red });
            MainViewport.Children.Add(new ArrowVisual3D { Point1 = new Point3D(0, -1.3, 0), Point2 = new Point3D(0, 1.3, 0), Diameter = 0.015, Fill = green });
            MainViewport.Children.Add(new ArrowVisual3D { Point1 = new Point3D(0, 0, -1.3), Point2 = new Point3D(0, 0, 1.3), Diameter = 0.015, Fill = blue });

            AddLabel("x  |+⟩", new Point3D(1.48, 0, 0), red.Color, 17);
            AddLabel("|−⟩", new Point3D(-1.18, 0, 0), red.Color, 13);
            AddLabel("y  |+j⟩", new Point3D(0, 1.5, 0), green.Color, 17);
            AddLabel("|−j⟩", new Point3D(0, -1.2, 0), green.Color, 13);
            AddLabel("z  |0⟩", new Point3D(0, 0, 1.45), blue.Color, 17);
            AddLabel("|1⟩", new Point3D(0, 0, -1.18), blue.Color, 15);
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

        private void AddLabel(string text, Point3D pos, Color color, double size)
        {
            MainViewport.Children.Add(new BillboardTextVisual3D
            {
                Text = text,
                Position = pos,
                Foreground = new SolidColorBrush(color),
                FontSize = size,
                FontWeight = FontWeights.Bold,
                Background = Brushes.Transparent
            });
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

            foreach (var track in _vm.Qubits)
            {
                bool selected = track == _vm.SelectedQubit;
                var steps = track.Steps.ToList();
                if (track == animTrack && animStep != null && steps.LastOrDefault() == animStep)
                    steps.RemoveAt(steps.Count - 1);
                if (steps.Count == 0) continue;

                int last = steps.Count - 1;

                if (_vm.ShowPaths)
                {
                    for (int i = 1; i <= last; i++)
                        AddPath(steps[i], track.Color, selected);
                }

                if (_vm.ShowHistory)
                {
                    for (int i = 0; i < last; i++)
                    {
                        // Older steps are fainter; the order is shown by the numbers.
                        double t = last <= 1 ? 1 : (double)i / (last - 1);
                        byte alpha = (byte)((selected ? 70 : 45) + t * (selected ? 90 : 50));
                        AddArrow(steps[i].Bloch, Color.FromArgb(alpha, track.Color.R, track.Color.G, track.Color.B), selected ? 0.018 : 0.012, 0.08);
                        if (selected) AddStepNumber(steps[i].Bloch, i, track.Color);
                    }
                }

                AddCurrentArrow(steps[last].Bloch, track, selected);
                if (selected && _vm.ShowHistory && last > 0) AddStepNumber(steps[last].Bloch, last, track.Color);
            }
        }

        private void AddCurrentArrow(Point3D tip, QubitTrack track, bool selected)
        {
            AddArrow(tip, track.Color, selected ? 0.045 : 0.03, selected ? 0.18 : 0.14);
            _dynamicRoot.Children.Add(new SphereVisual3D { Center = tip, Radius = selected ? 0.04 : 0.03, Fill = track.Brush });
            if (_vm.Qubits.Count > 1)
            {
                _dynamicRoot.Children.Add(new BillboardTextVisual3D
                {
                    Text = track.Name,
                    Position = Scale(tip, 1.16),
                    Foreground = track.Brush,
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Background = new SolidColorBrush(Color.FromArgb(170, 255, 255, 255))
                });
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

        private void AddPath(QubitStep step, Color color, bool selected)
        {
            if (step.Path.Count < 2) return;
            var pts = new Point3DCollection();
            for (int i = 0; i < step.Path.Count - 1; i++)
            {
                // Dashed look for measurement jumps, solid arcs for gates
                if (step.Kind == StepKind.Measurement && i % 2 == 1) continue;
                pts.Add(step.Path[i]);
                pts.Add(step.Path[i + 1]);
            }
            _dynamicRoot.Children.Add(new LinesVisual3D
            {
                Points = pts,
                Color = selected ? color : Color.FromArgb(150, color.R, color.G, color.B),
                Thickness = selected ? 2.4 : 1.4
            });
        }

        private void AddStepNumber(Point3D tip, int index, Color color)
        {
            _dynamicRoot.Children.Add(new BillboardTextVisual3D
            {
                Text = index.ToString(),
                Position = Scale(tip, 1.08),
                Foreground = Brushes.White,
                Background = new SolidColorBrush(color),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(3, 0, 3, 0)
            });
        }

        // ===================== Animation =====================

        private void OnStepAdded(object? sender, StepAddedEventArgs e)
        {
            StopAnimation();
            if (!_vm.AnimationEnabled || e.Step.Path.Count < 2)
            {
                RedrawScene();
                return;
            }

            _animTrack = e.Track;
            _animStep = e.Step;
            _animDurationMs = _vm.AnimationSeconds * 1000 * (e.Step.Kind == StepKind.Measurement ? 0.6 : 1.0);
            RedrawScene(_animTrack, _animStep);

            // Show the rotation axis of the gate while it acts
            if (e.Step.Kind == StepKind.Gate)
            {
                var axis = e.Step.Axis;
                if (axis.HasValue)
                {
                    var a = axis.Value * 1.35;
                    _dynamicRoot.Children.Add(new LinesVisual3D
                    {
                        Points = DashedLine(new Point3D(-a.X, -a.Y, -a.Z), new Point3D(a.X, a.Y, a.Z), 30),
                        Color = Color.FromRgb(90, 90, 90),
                        Thickness = 1.6
                    });
                }
            }

            _animPathLine = new LinesVisual3D { Color = e.Track.Color, Thickness = 2.4 };
            _animArrow = new ArrowVisual3D { Point1 = new Point3D(0, 0, 0), Point2 = e.Step.Path[0], Diameter = 0.045, HeadLength = 0.18 / 0.045, Fill = e.Track.Brush };
            _dynamicRoot.Children.Add(_animPathLine);
            _dynamicRoot.Children.Add(_animArrow);

            _animClock.Restart();
            CompositionTarget.Rendering += OnAnimationFrame;
        }

        private void OnAnimationFrame(object? sender, EventArgs e)
        {
            if (_animStep == null || _animArrow == null || _animPathLine == null) return;

            double t = Math.Min(1.0, _animClock.Elapsed.TotalMilliseconds / _animDurationMs);
            double eased = 0.5 - 0.5 * Math.Cos(Math.PI * t); // ease in-out
            var path = _animStep.Path;
            double pos = eased * (path.Count - 1);
            int idx = (int)Math.Floor(pos);
            idx = Math.Clamp(idx, 0, path.Count - 2);
            double frac = pos - idx;
            var a = path[idx];
            var b = path[idx + 1];
            var tip = new Point3D(a.X + (b.X - a.X) * frac, a.Y + (b.Y - a.Y) * frac, a.Z + (b.Z - a.Z) * frac);

            if (Length(tip) > 1e-3) _animArrow.Point2 = tip;

            if (_vm.ShowPaths)
            {
                var pts = new Point3DCollection();
                for (int i = 0; i < idx; i++)
                {
                    pts.Add(path[i]);
                    pts.Add(path[i + 1]);
                }
                pts.Add(path[idx]);
                pts.Add(tip);
                _animPathLine.Points = pts;
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
            _animTrack = null;
            _animStep = null;
            _animArrow = null;
            _animPathLine = null;
        }

        private static Point3DCollection DashedLine(Point3D a, Point3D b, int dashes)
        {
            var pts = new Point3DCollection();
            for (int i = 0; i < dashes; i += 2)
            {
                double t1 = (double)i / dashes, t2 = (double)(i + 1) / dashes;
                pts.Add(new Point3D(a.X + (b.X - a.X) * t1, a.Y + (b.Y - a.Y) * t1, a.Z + (b.Z - a.Z) * t1));
                pts.Add(new Point3D(a.X + (b.X - a.X) * t2, a.Y + (b.Y - a.Y) * t2, a.Z + (b.Z - a.Z) * t2));
            }
            return pts;
        }

        private static double Length(Point3D p) => Math.Sqrt(p.X * p.X + p.Y * p.Y + p.Z * p.Z);
        private static Point3D Scale(Point3D p, double s) => new(p.X * s, p.Y * s, p.Z * s);
    }
}
