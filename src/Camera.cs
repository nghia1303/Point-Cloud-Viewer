using OpenTK;
using OpenTK.Platform;
using System;

namespace PointCloudViewer.src
{
    // TL;DR: This is just one of many ways in which we could have set up the camera.
    // Check out the web version if you don't know why we are doing a specific thing or want to know more about the code.
    public class Camera
    {
        // Those vectors are directions pointing outwards from the camera to define how it rotated.
        private Vector3 _front = Vector3.UnitZ;

        private Vector3 _up = -Vector3.UnitY;

        private Vector3 _right = Vector3.UnitX;

        private Vector3 orbitCenter = Vector3.Zero;
        // Rotation around the X axis (radians)
        private float _pitch;

        // Rotation around the Y axis (radians)
        private float _yaw = -MathHelper.PiOver2; // Without this, you would be started rotated 90 degrees right.

        // The field of view of the camera (radians)
        private float _fov = MathHelper.PiOver2;

        public float zNear = 0.001f;
        public float zFar = 100f;
        public Camera(Vector3 position, float aspectRatio)
        {
            Position = position;
            AspectRatio = aspectRatio;
        }

        // The position of the camera
        public Vector3 Position;

        // This is simply the aspect ratio of the viewport, used for the projection matrix.
        public float AspectRatio { get; set; }

        public Vector3 Front => _front;

        public Vector3 Up => _up;

        public Vector3 Right => _right;

        public float Zoom = 45;

        // We convert from degrees to radians as soon as the property is set to improve performance.
        public float Pitch
        {
            get => MathHelper.RadiansToDegrees(_pitch);
            set
            {
                // We clamp the pitch value between -89 and 89 to prevent the camera from going upside down, and a bunch
                // of weird "bugs" when you are using euler angles for rotation.
                // If you want to read more about this you can try researching a topic called gimbal lock
                var angle = MathHelper.Clamp(value, -89f, 89f);
                _pitch = MathHelper.DegreesToRadians(angle);
                UpdateVectors();
            }
        }
        // We convert from degrees to radians as soon as the property is set to improve performance.
        public float Yaw
        {
            get => MathHelper.RadiansToDegrees(_yaw);
            set
            {
                _yaw = MathHelper.DegreesToRadians(value);
                UpdateVectors();
            }
        }
        // The field of view (FOV) is the vertical angle of the camera view.
        // This has been discussed more in depth in a previous tutorial,
        // but in this tutorial, you have also learned how we can use this to simulate a zoom feature.
        // We convert from degrees to radians as soon as the property is set to improve performance.
        public float Fov
        {
            get => MathHelper.RadiansToDegrees(_fov);
            set
            {
                var angle = MathHelper.Clamp(value, 1f, 90f);
                _fov = MathHelper.DegreesToRadians(angle);
            }
        }
        // Get the view matrix using the amazing LookAt function described more in depth on the web tutorials
        public Matrix4 GetViewMatrix()
        {
            return Matrix4.LookAt(Position, Position + Front, _up);
        }
        // Get the projection matrix using the same method we have used up until this point
        public Matrix4 GetProjectionMatrix()
        {
            return Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(Zoom), AspectRatio, zNear, zFar);
        }
        public Matrix4 GetProjectionMatrixOrtho(float width, float height)
        {
            return Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(Zoom), AspectRatio, zNear, zFar);
            //return Matrix4.CreateOrthographicOffCenter(-1, 1, 1, -1, -1, 10);
        }
        // This function is going to update the direction vertices using some of the math learned in the web tutorials.
        public void UpdateVectors()
        {
            // First, the front matrix is calculated using some basic trigonometry.
            _front.X = (float)(Math.Cos(_pitch) * Math.Cos(_yaw));
            _front.Y = (float)Math.Sin(_pitch);
            _front.Z = (float)(Math.Cos(_pitch) * Math.Sin(_yaw));

            // We need to make sure the vectors are all normalized, as otherwise we would get some funky results.
            _front = Vector3.Normalize(_front);

            // Calculate both the right and the up vector using cross product.
            // Note that we are calculating the right from the global up; this behaviour might
            // not be what you need for all cameras so keep this in mind if you do not want a FPS camera.
            _right = Vector3.Normalize(Vector3.Cross(_front, _up));
            _up = Vector3.Normalize(Vector3.Cross(_right, _front));
        }
    }
    public class Transform
    {
        // Local space information
        private Vector3 m_pos = Vector3.Zero;
        private Vector3 m_eulerRot = Vector3.Zero; // In degrees
        private Vector3 m_scale = Vector3.One;

        // Global space information concatenate in matrix
        private Matrix4 m_modelMatrix = Matrix4.Identity;
        private bool m_isDirty = true;

        // Compute the local model matrix
        private Matrix4 GetLocalModelMatrix()
        {
            var transformX = Matrix4.CreateRotationX(MathHelper.DegreesToRadians(m_eulerRot.X));
            var transformY = Matrix4.CreateRotationY(MathHelper.DegreesToRadians(m_eulerRot.Y));
            var transformZ = Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(m_eulerRot.Z));

            // Y * X * Zs
            var rotationMatrix = transformY * transformX * transformZ;

            // Translation * rotation * scale (TRS matrix)
            return Matrix4.CreateTranslation(m_pos) * rotationMatrix * Matrix4.CreateScale(m_scale);
        }

        public void ComputeModelMatrix()
        {
            m_modelMatrix = GetLocalModelMatrix();
            m_isDirty = false;
        }

        public void ComputeModelMatrix(Matrix4 parentGlobalModelMatrix)
        {
            m_modelMatrix = parentGlobalModelMatrix * GetLocalModelMatrix();
            m_isDirty = false;
        }

        public void SetLocalPosition(Vector3 newPosition)
        {
            m_pos = newPosition;
            m_isDirty = true;
        }

        public void SetLocalRotation(Vector3 newRotation)
        {
            m_eulerRot = newRotation;
            m_isDirty = true;
        }

        public void SetLocalScale(Vector3 newScale)
        {
            m_scale = newScale;
            m_isDirty = true;
        }

        public Vector3 GetGlobalPosition()
        {
            return new Vector3(m_modelMatrix.M41, m_modelMatrix.M42, m_modelMatrix.M43);
        }

        public Vector3 GetLocalPosition()
        {
            return m_pos;
        }

        public Vector3 GetLocalRotation()
        {
            return m_eulerRot;
        }

        public Vector3 GetLocalScale()
        {
            return m_scale;
        }

        public Matrix4 GetModelMatrix()
        {
            return m_modelMatrix;
        }

        public Vector3 GetRight()
        {
            return new Vector3(m_modelMatrix.M11, m_modelMatrix.M12, m_modelMatrix.M13);
        }

        public Vector3 GetUp()
        {
            return new Vector3(m_modelMatrix.M21, m_modelMatrix.M22, m_modelMatrix.M23);
        }

        public Vector3 GetBackward()
        {
            return new Vector3(m_modelMatrix.M31, m_modelMatrix.M32, m_modelMatrix.M33);
        }

        public Vector3 GetForward()
        {
            return -new Vector3(m_modelMatrix.M31, m_modelMatrix.M32, m_modelMatrix.M33);
        }

        public Vector3 GetGlobalScale()
        {
            return new Vector3(GetRight().Length, GetUp().Length, GetBackward().Length);
        }

        public bool IsDirty()
        {
            return m_isDirty;
        }
    }
}