using Amplitude.Mercury.Data.Presentation;
using Amplitude.Mercury.Fx.HgFx;
using Amplitude.Mercury.Presentation;
using HarmonyLib;
using Criteria = Amplitude.Mercury.Data.Presentation.AssetReferenceRepository.CriteriaEnum;

namespace CityRedraw.Diagnostics
{
    // SetChannel looks the art up by tags (district, culture style, era...) and an empty guid draws nothing
    // without any log, which is how a city can stand on bare ground. Read-only: repeats the lookup and logs
    // each tag combination that finds no art.
    [HarmonyPatch(typeof(PresentationLevelBuildComponent), nameof(PresentationLevelBuildComponent.SetChannel),
        new[] { typeof(int), typeof(AssetReferenceRepositoryRequest), typeof(HgFxAnchorComponent.RenderModeEnum), typeof(bool) },
        new[] { ArgumentType.Normal, ArgumentType.Ref, ArgumentType.Normal, ArgumentType.Normal })]
    internal static class MissingArtProbe
    {
        private static readonly Criteria[] Logged =
        {
            Criteria.District, Criteria.DistrictType, Criteria.DistrictState, Criteria.ConstructibleVisualAffinity,
            Criteria.BuildingVisualAffinity, Criteria.InitialBuildingVisualAffinity, Criteria.Era, Criteria.EntityType,
            Criteria.SettlementEvolution, Criteria.ArtificialWonder, Criteria.HolySite, Criteria.StatusCategorySettlement,
        };

        private static void Postfix(ref AssetReferenceRepositoryRequest request)
        {
            var copy = request;
            Plugin.MissingArtHook.Run(copy, static request =>
            {
                if (AssetReferenceRepository.Instance().GetGuid(ref request, out var guid) && !guid.Equals(Amplitude.Framework.Guid.Null)) return;
                var key = Describe(ref request);
                if (Plugin.MissingArt.ShouldReport(key)) Plugin.Log("no art for: " + key);
            });
        }

        private static string Describe(ref AssetReferenceRepositoryRequest request)
        {
            var text = request.DatabaseName.ToString();
            foreach (var criteria in Logged)
            {
                var value = request.GetCriterionValue(criteria).ToString();
                if (!string.IsNullOrEmpty(value)) text += " " + criteria + "=" + value;
            }

            return text;
        }
    }
}
