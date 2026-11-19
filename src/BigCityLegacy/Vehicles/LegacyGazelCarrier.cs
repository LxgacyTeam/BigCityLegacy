using System.Collections.Generic;
using UnityEngine;

internal sealed class LegacyGazelCarrier : MonoBehaviour
{
    internal static readonly HashSet<LegacyGazelCarrier> Active = new HashSet<LegacyGazelCarrier>();
    internal static readonly Dictionary<CarControl, LegacyGazelCarrier> Attached = new Dictionary<CarControl, LegacyGazelCarrier>();
    internal CarControl Truck { get; private set; }
    internal CarControl Cargo { get; private set; }
    internal Vector3 LocalPosition { get; private set; }
    internal Quaternion LocalRotation { get; private set; }
    private readonly List<(Collider Truck, Collider Cargo)> ignored = new List<(Collider, Collider)>();
    private bool wasKinematic;

    private void OnEnable()
    {
        Truck = GetComponent<CarControl>();
        Active.Add(this);
    }

    private void OnDisable()
    {
        Active.Remove(this);
        Release();
    }

    internal bool CanLoad(CarControl car)
    {
        if (Cargo || !Truck || !Truck.isAlive || !Truck.body || !car || !car.isAlive || !car.body ||
            car == Truck || car.GetComponent<LegacyGazelCarrier>() || Attached.ContainsKey(car) ||
            (Truck.body_linearW - car.body_linearW).sqrMagnitude > 2.25f ||
            Truck.body_linearW.sqrMagnitude > 4f)
            return false;

        Quaternion inverse = Quaternion.Inverse(Truck.body.rotation);
        Vector3 local = inverse * (car.body.position - Truck.body.position);
        Vector3 up = inverse * (car.body.rotation * Vector3.up);
        Vector3 forward = inverse * (car.body.rotation * Vector3.forward);
        if (up.y < 0.75f || Mathf.Abs(forward.z) < 0.85f || local.y < 0.25f || local.y > 1.95f)
            return false;

        if (!car.ph_car || car.ph_car.wheels == null || car.ph_car.wheels.Length < 4)
            return false;

        foreach (var wheel in car.ph_car.wheels)
        {
            if (!wheel) return false;
            Vector3 wheelPosition = car.body.position + car.body.rotation * wheel.create_pos;
            Vector3 point = inverse * (wheelPosition - Truck.body.position);
            if (Mathf.Abs(point.x) > 1.06f || point.z < -3.25f || point.z > 1.45f)
                return false;
        }
        return true;
    }

    internal void Lock(CarControl car, Vector3 localPosition, Quaternion localRotation)
    {
        if (Cargo == car || !Truck.body || !car.body)
            return;

        Release();
        if (Attached.TryGetValue(car, out var previous)) previous.Release();
        Cargo = car;
        LocalPosition = localPosition;
        LocalRotation = localRotation;
        Attached[car] = this;
        wasKinematic = car.body.isKinematic;
        car.body.isKinematic = true;

        foreach (var truckCollider in Truck.body.GetComponentsInChildren<Collider>())
        foreach (var carCollider in car.body.GetComponentsInChildren<Collider>())
        {
            if (truckCollider.isTrigger || carCollider.isTrigger ||
                Physics.GetIgnoreCollision(truckCollider, carCollider))
                continue;
            Physics.IgnoreCollision(truckCollider, carCollider);
            ignored.Add((truckCollider, carCollider));
        }
        ApplyPose();
    }

    internal void Release()
    {
        var car = Cargo;
        Cargo = null;
        if (!ReferenceEquals(car, null)) Attached.Remove(car);
        foreach (var pair in ignored)
        {
            if (pair.Truck && pair.Cargo)
                Physics.IgnoreCollision(pair.Truck, pair.Cargo, false);
        }
        ignored.Clear();

        if (!car || !car.body) return;
        car.body.isKinematic = wasKinematic;
        if (!wasKinematic && car.isActiveAndEnabled && Truck && Truck.body)
        {
            car.body.velocity = Truck.body.GetPointVelocity(car.body.position);
            car.body.angularVelocity = Truck.body.angularVelocity;
        }
    }

    private void LateUpdate()
    {
        if (ReferenceEquals(Cargo, null)) return;
        if (Truck && Truck.isAlive && Cargo && Cargo.isAlive && Cargo.isActiveAndEnabled)
            ApplyPose();
        else
            Release();
    }

    private void FixedUpdate()
    {
        if (Cargo) ApplyPose();
    }

    private void ApplyPose()
    {
        var body = Cargo.body;
        var carrier = Truck.body;
        if (!body || !carrier) return;

        Vector3 pos = carrier.position + carrier.rotation * LocalPosition;
        Quaternion rot = carrier.rotation * LocalRotation;
        body.position = pos;
        body.rotation = rot;
        body.transform.SetPositionAndRotation(pos, rot);
        Cargo.UpBodyInfo();
        Cargo.SetViewPosOnBody();
    }
}
