using System.Globalization;
using System.Xml.Linq;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class NativeOutputObservationBoundary
{
    private static bool PickupStackMatches(PendingPickupAdmission admission, NativeObservedInventoryPayloadSlot slot, XElement resident)
    {
        if (admission.Item.RuntimeType != slot.RuntimeType || admission.Item.QualifiedItemId != slot.QualifiedItemId ||
            admission.Item.Quality != slot.Quality) return false;
        // Item.canStackWith on the pinned vanilla Object compares name and orderData,
        // not arbitrary modData or the whole serialized unit. Resident metadata survives.
        return ItemXmlValue(admission.Xml, "name") == ItemXmlValue(resident, "name") &&
            XNode.DeepEquals(PickupOptionalScalar(admission.Xml, "orderData"), PickupOptionalScalar(resident, "orderData"));
    }

    private static void ApplyPickupReceiveFlags(XElement resident, XElement incoming)
    {
        Require(bool.TryParse(ItemXmlValue(incoming, "hasBeenInInventory"), out var received),
            "pickup input lacks its original receive-history flag");
        if (received) return;
        var flag = PickupOptionalScalar(incoming, "setFlagOnPickup");
        Require(flag is null || flag.Attribute(XName.Get("nil", "http://www.w3.org/2001/XMLSchema-instance"))?.Value == "true",
            "pickup with a non-null mail flag requires its explicit receive-side-effect settlement");
        var history = resident.Elements("hasBeenInInventory").ToArray();
        Require(history.Length == 1 && !history[0].HasElements && bool.TryParse(history[0].Value, out _),
            "pickup resident receive-history flag is unavailable");
        history[0].Value = "true";
    }

    private static XElement? PickupOptionalScalar(XElement root, string name)
    {
        var fields = root.Elements(name).ToArray();
        Require(fields.Length <= 1 && fields.All(field => !field.HasElements), "pickup XML scalar repeats or is nested: " + name);
        return fields.SingleOrDefault();
    }

    private static void SetPickupXmlStack(XElement root, int count)
    {
        _ = ItemXmlValue(root, "stack");
        root.Element("stack")!.Value = count.ToString(CultureInfo.InvariantCulture);
    }
}
