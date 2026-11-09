
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Modding.Custom;

namespace TsdTechnologies.Server;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } =
        "com.nomadtribals.tsdtechnologies";

    public string Name { get; init; } =
        "TsdTechnologies";

    public string Author { get; init; } =
        "nomadtribals";

    public List<string>? Contributors { get; init; }

    public SemanticVersioning.Version Version { get; init; } =
        new("1.0.0");

    public SemanticVersioning.Range SptVersion { get; init; } =
        new("4.1.6");

    public List<string>? Incompatibilities { get; init; }

    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }

    public string? Url { get; init; }

    public string License { get; init; } = "MIT";

    public bool HasPrepatcher { get; init; } = false;
}

[Injectable(TypePriority = OnLoadOrder.Preload)]
public class TechnologiesMod(ISptLogger<TechnologiesMod> logger, TemplateTable templateTable, CustomItemService customItemService, TradersTable tradersTable) : IOnLoad
{
    private const string TsdFieldTerminalId =
        "6a0000000000000000000002";

    private const string TsdFieldTerminalOfferId =
        "6a0000000000000000000003";

    private const string MechanicTraderId =
        "5a7c2eca46aef81a7ca2145d";

    private const string SpecialItemBaseId =
        "5447e0e74bdc2d3c308b4567";

    private const string RadioTransmitterBodyId =
        "6399f54b0a36db13c823ad21";

    private const string RoublesId =
        "5449016a4bdc2d6f028b456f";

    private const string HandbookParentId = "5b47574386f77428ca22b33b";
    public Task OnLoadAsync(CancellationToken cancellationToken) { RegisterFieldTerminal(); logger.Success("TSD Technologies loaded."); return Task.CompletedTask; }
    private bool RegisterFieldTerminal()
    {
        MongoId terminalId = new(TsdFieldTerminalId);

        if (!templateTable.Items.ContainsKey(terminalId))
        {
            if (!templateTable.Items.ContainsKey(
                new MongoId(RadioTransmitterBodyId)))
            {
                logger.Error(
                    "[TSD] Cannot register the field terminal. " +
                    "The vanilla radio transmitter template was not found.");

                return false;
            }

            var itemDetails = new NewItemFromCloneDetails
            {
                ItemTplToClone = new MongoId(RadioTransmitterBodyId),
                NewId = terminalId,
                // The terminal is carried for F10 access; it has no radio hand animation.
                ParentId = new MongoId(SpecialItemBaseId),
                NewItemName = "tsd_field_terminal",
                AddToHandbook = true,
                AddToFleaPriceDb = false,
                AddToWeaponShelf = false,
                HandbookParentId = HandbookParentId,
                HandbookPriceRoubles = 75000,
                OverrideProperties = new TemplateItemProperties
                {
                    Name = "TSD Field Terminal",
                    ShortName = "TSD Terminal",
                    Description =
                        "A hardened personal field terminal from TSD Technologies. " +
                        "Runs Field OS and installed applications. Carry it to access your workspace.",
                    // The transmitter body is a quest item; the terminal must be carryable.
                    QuestItem = false,
                    IsSpecialSlotOnly = false,
                    UnlootableFromSlot = "SpecialSlot",
                    CanSellOnRagfair = false,
                    Prefab = new Prefab
                    {
                        Path = "assets/content/items/equipment/tsd_field_terminal/tsd_field_terminal.bundle",
                        Rcid = ""
                    }
                },
                Locales = new Dictionary<string, LocaleDetails>
                {
                    ["en"] = new LocaleDetails
                    {
                        Name = "TSD Field Terminal",
                        ShortName = "TSD Terminal",
                        Description =
                            "A hardened TSD Technologies tablet running Field OS and your installed apps."
                    }
                }
            };

            var result = customItemService.CreateItemFromClone(
                itemDetails,
                typeof(TechnologiesMod).Assembly);

            if (!result.Success)
            {
                logger.Error(
                    "[TSD] Field terminal registration failed: " +
                    string.Join("; ", result.Errors));

                return false;
            }
        }

        AddFieldTerminalToSpecialSlots();
        AddFieldTerminalToMechanic();
        return true;
    }

    private void AddFieldTerminalToSpecialSlots()
    {
        MongoId terminalId = new(TsdFieldTerminalId);
        int patched = 0;
        foreach (var template in templateTable.Items.Values)
        {
            var slots = template.Properties?.Slots;
            if (slots == null) continue;
            foreach (var slot in slots)
            {
                if (slot.Name is not ("SpecialSlot1" or "SpecialSlot2" or "SpecialSlot3"))
                    continue;
                var filters = slot.Properties?.Filters;
                if (filters == null) continue;
                foreach (var filter in filters)
                {
                    // Keep the eligibility previously inherited from the radio category.
                    if (filter?.Filter?.Contains(new MongoId("62e9103049c018f425059f38")) == true
                        && filter.Filter.Add(terminalId)) patched++;
                }
            }
        }
        logger.Info($"[TSD] Field terminal enabled in {patched} special-slot filters.");
    }

    private void AddFieldTerminalToMechanic()
    {
        MongoId mechanicId = new(MechanicTraderId);
        MongoId offerId = new(TsdFieldTerminalOfferId);

        if (!tradersTable.TryGetValue(mechanicId, out var mechanic) ||
            mechanic?.Assort == null)
        {
            logger.Error(
                "[TSD] Could not add the field terminal to Mechanic: " +
                "his trader assortment was not available during mod loading.");

            return;
        }

        var assort = mechanic.Assort;
        if (assort.Items == null)
        {
            logger.Error(
                "[TSD] Mechanic's assortment has no item list.");

            return;
        }

        if (!assort.Items.Any(item => item.Id == offerId))
        {
            assort.Items.Add(new Item
            {
                Id = offerId,
                Template = new MongoId(TsdFieldTerminalId),
                ParentId = "hideout",
                SlotId = "hideout",
                Upd = new Upd
                {
                    UnlimitedCount = true,
                    StackObjectsCount = 9999999
                }
            });
        }

        assort.BarterScheme[offerId] = new List<List<BarterScheme>>
        {
            new()
            {
                new BarterScheme
                {
                    Count = 75000,
                    Template = new MongoId(RoublesId)
                }
            }
        };
        assort.LoyalLevelItems[offerId] = 1;

        logger.Success(
            "[TSD] TSD Field Terminal added to Mechanic at Loyalty Level 1 " +
            "for 75,000 roubles. It fits the Special slot but does not " +
            "require it.");
    }

}
