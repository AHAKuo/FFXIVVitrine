using Glamourer.Api.Enums;
using Newtonsoft.Json.Linq;
using Vitrine;
using Vitrine.Designs;
using Vitrine.Ipc;
using Xunit;

namespace Vitrine.Tests;

public class DesignJsonApplyActionTests
{
    private static VitrineDesign WithAction()
    {
        var action = new ApplyAction();
        action.Slots[ApiEquipSlot.Body] = new ApplySlot { Enabled = true, ItemId = 1001, ItemName = "Cow Top" };
        action.Slots[ApiEquipSlot.Legs] = new ApplySlot { Enabled = true };
        action.Slots[ApiEquipSlot.Head] = new ApplySlot { Enabled = false, ItemId = 7, ItemName = "Hat" };
        return new VitrineDesign
        {
            Id          = Guid.Parse("a0f95146-309c-544e-9225-4702f2c86cb1"),
            Name        = "Cowkini",
            IsAuto      = true,
            Category    = VitrineCategory.Gear,
            Mods        = { new VitrineModState { ModDirectory = "Cowkini", ModName = "Cowkini" } },
            ApplyAction = action,
        };
    }

    [Fact]
    public void ApplyAction_IsCamelCase_WithSlotNamesAsKeys()
    {
        var json = JObject.Parse(DesignJson.Serialize(WithAction()));
        var slots = (JObject)json["applyAction"]!["slots"]!;

        Assert.Equal(new[] { "Body", "Legs", "Head" }, slots.Properties().Select(p => p.Name));
        Assert.Equal(1001, slots["Body"]!["itemId"]!.Value<long>());
        Assert.Equal("Cow Top", slots["Body"]!["itemName"]!.Value<string>());
        Assert.True(slots["Body"]!["enabled"]!.Value<bool>());
        Assert.Equal(JTokenType.Null, slots["Legs"]!["itemId"]!.Type);
        Assert.False(slots["Head"]!["enabled"]!.Value<bool>());
    }

    [Fact]
    public void ApplyAction_RoundTrips()
    {
        var back = DesignJson.Deserialize(DesignJson.Serialize(WithAction()));

        Assert.NotNull(back?.ApplyAction);
        Assert.Equal(1001UL, back!.ApplyAction!.Slots[ApiEquipSlot.Body].ItemId);
        Assert.Null(back.ApplyAction.Slots[ApiEquipSlot.Legs].ItemId);
        Assert.False(back.ApplyAction.Slots[ApiEquipSlot.Head].Enabled);
    }

    [Fact]
    public void DesignWithoutAction_SerialisesNull()
    {
        var design = WithAction();
        design.ApplyAction = null;
        var json = JObject.Parse(DesignJson.Serialize(design));

        Assert.Equal(JTokenType.Null, json["applyAction"]!.Type);
    }

    [Fact]
    public void Store_KeepsActionsOnBaselinesAndVariants_AndClonesThem()
    {
        var path  = Path.Combine(Path.GetTempPath(), $"vitrine-{Guid.NewGuid():N}.json");
        try
        {
            var store = new VitrineDesignStore(path, _ => { });
            store.Load();
            store.UpdateFromCatalog(new[] { new ModInformation { ModDirectory = "Cowkini", Name = "Cowkini", Type = VitrineCategory.Gear } });

            var action = new ApplyAction();
            action.Slots[ApiEquipSlot.Body] = new ApplySlot { ItemId = 1001, ItemName = "Cow Top" };
            store.SetBaseline("Cowkini", new Baseline { Priority = 1, ApplyAction = action });

            var auto = store.AutoDesignFor("Cowkini")!;
            Assert.Equal("Cow Top", auto.ApplyAction!.Slots[ApiEquipSlot.Body].ItemName);
            Assert.NotSame(action, auto.ApplyAction);

            var variantId = store.CreateVariant("Cowkini", "V");
            Assert.Equal("Cow Top", store.Get(variantId)!.ApplyAction!.Slots[ApiEquipSlot.Body].ItemName);

            var changed = new ApplyAction();
            changed.Slots[ApiEquipSlot.Body] = new ApplySlot { Enabled = false, ItemName = "Cow Top" };
            Assert.True(store.UpdateVariant(variantId, applyAction: changed));
            Assert.False(store.Get(variantId)!.ApplyAction!.Slots[ApiEquipSlot.Body].Enabled);

            Assert.True(store.UpdateVariant(variantId, clearApplyAction: true));
            Assert.Null(store.Get(variantId)!.ApplyAction);

            // Baseline action survives a reload.
            var reloaded = new VitrineDesignStore(path, _ => { });
            reloaded.Load();
            reloaded.UpdateFromCatalog(new[] { new ModInformation { ModDirectory = "Cowkini", Name = "Cowkini", Type = VitrineCategory.Gear } });
            Assert.Equal(1001UL, reloaded.AutoDesignFor("Cowkini")!.ApplyAction!.Slots[ApiEquipSlot.Body].ItemId);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
