using MathNet.Numerics.LinearAlgebra;
using System;
using System.Windows.Media.Media3D;
using Complex = System.Numerics.Complex;

namespace BlochSphereSimulator.Models
{
    public static class QuantumMath
    {
        // Pauli Matrices and Hadamard Matrix

        /*
         *  [0,  1] 
         *  [1,  0] 
         */
        public static Matrix<Complex> X() => Matrix<Complex>.Build.DenseOfArray(new Complex[,] { { 0, 1 }, { 1, 0 } });

        /*
         *  [1,  0] 
         *  [0, -1] 
         */
        public static Matrix<Complex> Z() => Matrix<Complex>.Build.DenseOfArray(new Complex[,] { { 1, 0 }, { 0, -1 } });

        /*
         *  [0, -j] 
         *  [j,  0] 
         */
        public static Matrix<Complex> Y() => Matrix<Complex>.Build.DenseOfArray(new Complex[,] { { 0, new Complex(0, -1) }, { new Complex(0, 1), 0 } });

        /*
         *  [1/sqrt(2), 1/sqrt(2)] 
         *  [1/sqrt(2), -1/sqrt(2)] 
         */
        public static Matrix<Complex> H() => Matrix<Complex>.Build.DenseOfArray(new Complex[,] {
            { 1 / Math.Sqrt(2), 1 / Math.Sqrt(2) },
            { 1 / Math.Sqrt(2), -1 / Math.Sqrt(2) }
        });

        // Fáziskapu: P(alpha)
        /*
         *  [1,       0     ]
         *  [0,  e^(j*alpha)]
         */
        public static Matrix<Complex> Phase(double angleDeg)
        {
            double alpha = angleDeg * Math.PI / 180.0;
            return Matrix<Complex>.Build.DenseOfArray(new Complex[,] {
                { 1, 0 },
                { 0, Complex.Exp(new Complex(0, alpha)) }
            });
        }

        // Rotation Matrices around X, Y, Z axes

        public static Matrix<Complex> Rx(double angleDeg)
        {
            double t = angleDeg * Math.PI / 180.0;
            return Matrix<Complex>.Build.DenseOfArray(new Complex[,] {
                { Math.Cos(t / 2), new Complex(0, -Math.Sin(t / 2)) },
                { new Complex(0, -Math.Sin(t / 2)), Math.Cos(t / 2) }
            });
        }

        public static Matrix<Complex> Ry(double angleDeg)
        {
            double t = angleDeg * Math.PI / 180.0;
            return Matrix<Complex>.Build.DenseOfArray(new Complex[,] {
                { Math.Cos(t / 2), -Math.Sin(t / 2) },
                { Math.Sin(t / 2), Math.Cos(t / 2) }
            });
        }

        public static Matrix<Complex> Rz(double angleDeg)
        {
            double t = angleDeg * Math.PI / 180.0;
            return Matrix<Complex>.Build.DenseOfArray(new Complex[,] {
                { Complex.Exp(new Complex(0, -t / 2)), 0 },
                { 0, Complex.Exp(new Complex(0, t / 2)) }
            });
        }

        public static Vector<Complex> GetStateVector(double thetaDeg, double phiDeg)
        {
            double theta = thetaDeg * Math.PI / 180.0;
            double phi = phiDeg * Math.PI / 180.0;

            var alpha = Math.Cos(theta / 2.0);
            var beta = Complex.Exp(new Complex(0, phi)) * Math.Sin(theta / 2.0);

            return Vector<Complex>.Build.Dense(new[] { (Complex)alpha, (Complex)beta });
        }

        public static Point3D GetBlochCoordinates(Vector<Complex> state)
        {
            var alpha = state[0];
            var beta = state[1];

            double x = 2 * (Complex.Conjugate(alpha) * beta).Real;
            double y = 2 * (Complex.Conjugate(alpha) * beta).Imaginary;
            double z = Math.Pow(alpha.Magnitude, 2) - Math.Pow(beta.Magnitude, 2);

            return new Point3D(x, y, z);
        }
    }
}