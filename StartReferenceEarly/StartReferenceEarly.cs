using System.Reflection;
using System.Text.Json;
using JetBrains.Annotations;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using Path = System.IO.Path;

namespace StartReferenceEarly;

[UsedImplicitly]
[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class StartReferenceEarly(DatabaseServer dbServer, ModHelper modHelper, ISptLogger<StartReferenceEarly> logger) : IOnLoad
{
    private readonly JsonSerializerOptions _jsonSerializerOptions = new() { WriteIndented = true };
    private readonly MongoId _prereqQuest = QuestTpl.INFORMED_MEANS_ARMED;
    private List<VisibilityCondition> _visibilityConditions = [];
    private SREConfig? _config;

    public Task OnLoad()
    {
        var path = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var configPath = Path.Combine(path, "config.json");
        if (!File.Exists(configPath))
        {
            _config = new SREConfig();
            var json = JsonSerializer.Serialize(_config,  _jsonSerializerOptions);
            logger.Warning("[SRE] Creating new JSON config file due to it being missing, please re-run server after making changes...");
            File.WriteAllText(configPath, json);
        }
        _config = modHelper.GetJsonDataFromFile<SREConfig>(path, "config.json");
        var quests = dbServer.GetTables().Templates.Quests;
        if (!quests.TryGetValue(QuestTpl.IS_THIS_A_REFERENCE, out var quest)) return Task.CompletedTask;
        var conditions = new List<QuestCondition>();
        var originalConditions = quest.Conditions.AvailableForStart;
        foreach (var condition in originalConditions!)
        {
            switch (condition.ConditionType)
            {
                case "Level":
                {
                    condition.Value = _config.AvailableAfterLevel;
                    conditions.Add(condition);
                    break;
                }
                case "TraderStanding":
                {
                    condition.Value = 0.00;
                    conditions.Add(condition);
                    break;
                }
            }
        }
        if (conditions.Count == 0) return Task.CompletedTask;
        quest.Conditions.AvailableForStart = conditions;
        logger.Info($"[SRE] Successfully modified start conditions. Current level required is {_config.AvailableAfterLevel}.");
        return Task.CompletedTask;
    }
}