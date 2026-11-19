using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

internal sealed class LegacyGazelCarrierInput : MonoBehaviour
{
    private LegacyGazelCarrier promptedCarrier;
    private bool promptedRelease;

    private void Update()
    {
        LegacyGazelCarrierNetwork.Tick();
        if (NetManager.isServer) return;

        var input = InputControl.firstUser;
        var car = input ? input.currentOrParent as CarControl : null;
        var selection = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        var textField = selection ? selection.GetComponent<InputField>() : null;
        if (!car || car.GetHandleInput() != input || !GameUI.me ||
            (NetManager.isOnlineClient && !LegacyGazelCarrierNetwork.Available))
        {
            promptedCarrier = null;
            return;
        }
        if (MenuEsc.me || FlyCamera.me || WeaponeSelect.isStartSelect || (textField && textField.isFocused))
            return;

        var carrier = FindCarrier(car);
        if (!carrier)
        {
            promptedCarrier = null;
            return;
        }

        bool release = carrier.Cargo;
        bool russian = LegacyLocalizer.IsRussianLanguage;
        if (carrier != promptedCarrier || release != promptedRelease)
        {
            string action = release ? (russian ? "Открепить машину" : "Unlock vehicle") :
                (russian ? "Закрепить машину" : "Lock vehicle");
            GameUI.me.ShowPopup("[K] " + action, 2.5f, true);
            promptedCarrier = carrier;
            promptedRelease = release;
        }

        if (!Input.GetKeyDown(KeyCode.K)) return;
        if (NetManager.isOnlineClient)
            LegacyGazelCarrierNetwork.Request(carrier, release ? carrier.Cargo : car, release);
        else if (release)
            carrier.Release();
        else
        {
            Rigidbody truck = carrier.Truck.body;
            var inverse = Quaternion.Inverse(truck.rotation);
            carrier.Lock(car, inverse * (car.body.position - truck.position), inverse * car.body.rotation);
        }
    }

    private static LegacyGazelCarrier FindCarrier(CarControl car)
    {
        if (LegacyGazelCarrier.Attached.TryGetValue(car, out var attached))
            return attached;

        var ownCarrier = car.GetComponent<LegacyGazelCarrier>();
        if (ownCarrier && ownCarrier.Cargo) return ownCarrier;

        foreach (var candidate in LegacyGazelCarrier.Active)
        {
            if (candidate && candidate.CanLoad(car))
                return candidate;
        }
        return null;
    }
}
