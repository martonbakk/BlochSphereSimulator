using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using BlochSphereSimulator.ViewModels;

namespace BlochSphereSimulator
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            Application.Current.DispatcherUnhandledException += (s, e) =>
            {
                MessageBox.Show($"Kritikus UI hiba történt:\n{e.Exception.Message}\n\nRészletek:\n{e.Exception.StackTrace}",
                                "Összeomlás Megakadályozva", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
            };

            try
            {
                InitializeComponent();

                MainViewport.RotateGesture = new MouseGesture(MouseAction.LeftClick);
                MainViewport.PanGesture = new MouseGesture(MouseAction.RightClick);

                DrawWireframeSphere();

                var vm = new MainViewModel();
                this.DataContext = vm;

                vm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(vm.Arrows))
                        Update3DArrows(vm);
                };

                Update3DArrows(vm);

                MainViewport.CameraChanged += (s, e) => UpdateLabels();
                this.SizeChanged += (s, e) => UpdateLabels();
                Loaded += (s, e) => UpdateLabels();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hiba az indításkor:\n{ex.Message}", "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateLabels()
        {
            // BIZTONSÁGI JAVÍTÁS: Csak akkor rajzol, ha a nézet már betöltött és látható
            if (!this.IsLoaded || MainViewport.Viewport == null || MainViewport.Viewport.Camera == null || MainViewport.ActualWidth == 0)
                return;

            bool IsValid(Point p) => !double.IsNaN(p.X) && !double.IsInfinity(p.X) && !double.IsNaN(p.Y) && !double.IsInfinity(p.Y);

            try
            {
                var pX = Viewport3DHelper.Point3DtoPoint2D(MainViewport.Viewport, new Point3D(1.4, 0, 0));
                if (IsValid(pX)) { Canvas.SetLeft(LabelX, pX.X); Canvas.SetTop(LabelX, pX.Y); LabelX.Visibility = Visibility.Visible; }
                else LabelX.Visibility = Visibility.Hidden;

                var pY = Viewport3DHelper.Point3DtoPoint2D(MainViewport.Viewport, new Point3D(0, 1.4, 0));
                if (IsValid(pY)) { Canvas.SetLeft(LabelY, pY.X); Canvas.SetTop(LabelY, pY.Y); LabelY.Visibility = Visibility.Visible; }
                else LabelY.Visibility = Visibility.Hidden;

                var pZ = Viewport3DHelper.Point3DtoPoint2D(MainViewport.Viewport, new Point3D(0, 0, 1.4));
                if (IsValid(pZ)) { Canvas.SetLeft(LabelZ, pZ.X); Canvas.SetTop(LabelZ, pZ.Y); LabelZ.Visibility = Visibility.Visible; }
                else LabelZ.Visibility = Visibility.Hidden;
            }
            catch { /* Védvonal a layout motor hibái ellen */ }
        }

        private void DrawWireframeSphere()
        {
            var points = new Point3DCollection();

            int meridians = 16;
            int parallels = 12;

            for (int i = 0; i < meridians; i++)
            {
                double theta = i * Math.PI / meridians;
                int segments = 40;
                for (int j = 0; j < segments; j++)
                {
                    double phi1 = j * 2 * Math.PI / segments;
                    double phi2 = (j + 1) * 2 * Math.PI / segments;

                    points.Add(new Point3D(Math.Sin(phi1) * Math.Cos(theta), Math.Sin(phi1) * Math.Sin(theta), Math.Cos(phi1)));
                    points.Add(new Point3D(Math.Sin(phi2) * Math.Cos(theta), Math.Sin(phi2) * Math.Sin(theta), Math.Cos(phi2)));
                }
            }

            for (int i = 1; i < parallels; i++)
            {
                double phi = i * Math.PI / parallels;
                double z = Math.Cos(phi);
                double r = Math.Sin(phi);
                int segments = 40;
                for (int j = 0; j < segments; j++)
                {
                    double theta1 = j * 2 * Math.PI / segments;
                    double theta2 = (j + 1) * 2 * Math.PI / segments;

                    points.Add(new Point3D(r * Math.Cos(theta1), r * Math.Sin(theta1), z));
                    points.Add(new Point3D(r * Math.Cos(theta2), r * Math.Sin(theta2), z));
                }
            }

            var wireframe = new LinesVisual3D
            {
                Points = points,
                Color = Color.FromRgb(220, 220, 220),
                Thickness = 0.8
            };
            MainViewport.Children.Add(wireframe);

            var brushRed = new SolidColorBrush(Color.FromRgb(255, 107, 107));
            var brushGreen = new SolidColorBrush(Color.FromRgb(46, 204, 113));
            var brushBlue = new SolidColorBrush(Color.FromRgb(84, 160, 255));

            // BIZTONSÁGI JAVÍTÁS: Egyedi Materialok helyett az egyszerű Fill tulajdonságot használjuk a kompatibilitásért
            MainViewport.Children.Add(new ArrowVisual3D { Point1 = new Point3D(-1.3, 0, 0), Point2 = new Point3D(1.3, 0, 0), Diameter = 0.015, Fill = brushRed });
            MainViewport.Children.Add(new ArrowVisual3D { Point1 = new Point3D(0, -1.3, 0), Point2 = new Point3D(0, 1.3, 0), Diameter = 0.015, Fill = brushGreen });
            MainViewport.Children.Add(new ArrowVisual3D { Point1 = new Point3D(0, 0, -1.3), Point2 = new Point3D(0, 0, 1.3), Diameter = 0.015, Fill = brushBlue });
        }

        private void Update3DArrows(MainViewModel vm)
        {
            var arrowsToRemove = MainViewport.Children.OfType<ArrowVisual3D>()
                                .Where(a => a.Point1 == new Point3D(0, 0, 0)).ToList();

            foreach (var a in arrowsToRemove)
            {
                MainViewport.Children.Remove(a);
            }

            foreach (var arrowData in vm.Arrows)
            {
                bool isMainVector = arrowData.Thickness > 0.05;

                if (isMainVector)
                {
                    MainViewport.Children.Add(new ArrowVisual3D
                    {
                        Point1 = new Point3D(0, 0, 0),
                        Point2 = arrowData.Point,
                        Diameter = 0.04,
                        HeadLength = 0.18,
                        Fill = Brushes.Crimson
                    });

                    MainViewport.Children.Add(new ArrowVisual3D
                    {
                        Point1 = new Point3D(0, 0, 0),
                        Point2 = arrowData.Point,
                        Diameter = 0.06,
                        HeadLength = 0.20,
                        Fill = new SolidColorBrush(Color.FromArgb(60, 220, 20, 60))
                    });
                }
                else
                {
                    MainViewport.Children.Add(new ArrowVisual3D
                    {
                        Point1 = new Point3D(0, 0, 0),
                        Point2 = arrowData.Point,
                        Diameter = 0.015,
                        HeadLength = 0.08,
                        Fill = Brushes.LightGray
                    });
                }
            }
        }
    }
}