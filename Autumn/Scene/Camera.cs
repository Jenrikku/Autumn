﻿using System.Numerics;

// Based on SceneGL.Testing: https://github.com/jupahe64/SceneGL/blob/master/SceneGL.Testing/Camera.cs

namespace Autumn.Scene;

internal class Camera {
    public Vector3 Eye { get; set; }
    public Quaternion Rotation { get; set; }

    public Camera(Vector3 eye, Quaternion rotation) {
        Eye = eye;
        Rotation = rotation;
        _eyeAnimated = eye;
        _rotAnimated = rotation;
    }

    public Camera(Vector3 eye, Vector3 lookat) {
        LookAt(eye, lookat);

        _eyeAnimated = Eye;
        _rotAnimated = Rotation;
    }

    public void LookAt(Vector3 eye, Vector3 lookat) {
        Vector3 diff = eye - lookat;

        Quaternion rotation = Quaternion.CreateFromYawPitchRoll(MathF.Atan2(diff.X, diff.Z), -MathF.Asin(diff.Y / diff.Length()), 0);

        Eye = eye;
        Rotation = rotation;
    }

    public void Animate(double deltaTime, out Vector3 eyeAnimated, out Quaternion rotAnimated) {
        _rotAnimated = Quaternion.Slerp(_rotAnimated, Rotation, 1 - (float) Math.Pow(1 - 0.2, deltaTime * 60));

        _eyeAnimated = Vector3.Lerp(_eyeAnimated, Eye, 1 - (float) Math.Pow(1 - 0.2, deltaTime * 60));

        eyeAnimated = _eyeAnimated;
        rotAnimated = _rotAnimated;
    }

    private Vector3 _eyeAnimated;
    private Quaternion _rotAnimated;
}
