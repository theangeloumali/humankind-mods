#!/usr/bin/env bash
# Rebuilds, verifies and installs the ZKidz fix modules. Rerun it whenever AMC, Sigma Pack or
# exploitallFIMS updates: a patch holds copies of their units, so a stale one undoes the update.
set -euo pipefail
H="$(cd "$(dirname "$0")" && pwd)"
. "$H/../../scripts/lib.sh"
PY="$H/../.venv/bin/python"
OUT="$H/../build/fix-modules"
INSTALL="${ZK_INSTALL_DIR:-$HK_DOCS/Community/ZKidz}"
AMC="/mnt/d/SteamLibrary/steamapps/workshop/content/1124300/3307603664"
SIGMA="$(ls -d "$HK_DOCS"/Community/mod.io/*/Sigma\ Pack.* | head -n 1)"
FIMS="$(ls -d "$HK_DOCS"/Community/*/*/exploitallFIMS.* | head -n 1)"
EVENTFUL="$(ls -d "$HK_DOCS"/Community/*/*/Eventful.* | head -n 1)"
# Eventful applies unit statuses to armies, which hangs the turn (status_check.py); these point them at units.
EVENTFUL_EDITS=(--retarget-status NarrativeEvent_Neolithic05:NomadicFoodGained.Army=Units
  --add-units-variable NarrativeEvent_Neolithic05a:Armies=ArmyUnits --retarget-status NarrativeEvent_Neolithic05a:Armies=ArmyUnits
  --add-units-variable NarrativeEvent_Neolithic05b:Armies=ArmyUnits --retarget-status NarrativeEvent_Neolithic05b:Armies=ArmyUnits)

SUPER="/mnt/d/SteamLibrary/steamapps/workshop/content/1124300/2967595683"
MIC="/mnt/d/SteamLibrary/steamapps/workshop/content/1124300/3355586886"

AMC_SETS=(NavalUnit_Era2_PacificPolynesians_Waakaulua.Level=13 NavalTransport_Era2_PacificPolynesians_Waakaulua.Level=15)
SIGMA_SETS=(NavalUnit_Era2_Janggolan.Level=19 NavalUnit_Era2_Trireme.Level=19)
# Presentations with no figures can't be drawn, and the throw stops the map updating (presentation_check.py).
# Uybabaete and the Waakaulua dummy take over units that already have figures; the others get their own back.
SIGMA_PRESENTATION_SETS=(PresentationLandUnit_Era1_Uybabaete.IsWorkInProgress=1
  Era2_Cruithni_01.PresentationUnitDefinition=@PresentationLandUnit_Era2_Cruithni
  Era1_Alia_01.PresentationUnitDefinition=@PresentationNavalUnit_Era1_Alia)
SUPER_PRESENTATION_SETS=(Era4_Zurich_Reislaufer_01.PresentationUnitDefinition=@PresentationLandUnit_Era4_Zurich_Reislaufer)
# Effects compiled against the game's entity contracts (descriptor_check.py). These were written from another
# entity, so building them broke the turn (soak d, turn 54). Building yields stay on the building (descriptions);
# empire-only properties go to its empire; the Mekter boosts units like the base Pi-Ramesses Stables; the
# Seminole and the Song Junk carried effects of other kinds, which are dropped.
SIGMA_EFFECT_SETS=(
  'Effect_Extension_Era3_Wampanoag.Effects.0.Path.PropertyToFollow=json:[]'
  'Effect_Extension_Era3_Wampanoag.startingType=json:"Amplitude.Mercury.Simulation.District, Amplitude.Mercury.Firstpass"'
  'Effect_Extension_Era3_SuiDynasty.Effects.0.Path.PropertyToFollow=json:[]'
  'Effect_Extension_Era3_SuiDynasty.startingType=json:"Amplitude.Mercury.Simulation.District, Amplitude.Mercury.Firstpass"'
  'Effect_Extension_Era5_Peru.Effects.0.Path.PropertyToFollow=json:[]'
  'Effect_Extension_Era5_Peru.startingType=json:"Amplitude.Mercury.Simulation.District, Amplitude.Mercury.Firstpass"'
  'Effect_Extension_Era3_Armenia.Effects.0.Path.PropertyToFollow=json:[]'
  'Effect_Extension_Era3_Armenia.startingType=json:"Amplitude.Mercury.Simulation.District, Amplitude.Mercury.Firstpass"'
  'Effect_Extension_Era4_Portugal.Effects.0.Path.PropertyToFollow=json:["Settlement"]'
  'Effect_Extension_Era4_Portugal.Effects.0.ApplyEffectOnSource=0'
  'Effect_Extension_Era4_Portugal.Effects.0.PropertyEffects=json:[{"Note": "", "TargetProperty": "MoneyNet", "ToTargetOperation": 0, "RpnOperationStack": [10, 8, 2], "ConstantStack": [{"RawValue": 1000}], "PropertyLocalName": ["NavalTradeRoadsCount"]}]'
  'Effect_Extension_Era4_Portugal.Effects.1.Path.PropertyToFollow=json:[]'
  'Effect_Extension_Era4_Portugal.startingType=json:"Amplitude.Mercury.Simulation.District, Amplitude.Mercury.Firstpass"'
  'Effect_Extension_Era6_Myanmar_Synergy00.Effects.0.Path.PropertyToFollow=json:["Settlement","Empire"]'
  'Effect_Extension_Era6_Myanmar_Synergy00.Effects.0.Path.specificTargetType=json:"Amplitude.Mercury.Simulation.MajorEmpire, Amplitude.Mercury.Firstpass"'
  'Effect_Extension_Era6_Myanmar_Synergy00.Effects.0.ApplyEffectOnSource=0'
  'Effect_Extension_Era1_Hyksos.Effects.0.Path.PropertyToFollow=json:["Settlement","Empire","Units"]'
  'Effect_Extension_Era1_Hyksos.startingType=json:"Amplitude.Mercury.Simulation.District, Amplitude.Mercury.Firstpass"'
  'Effect_Extension_Era6_Myanmar_Synergy00.startingType=json:"Amplitude.Mercury.Simulation.District, Amplitude.Mercury.Firstpass"'
  'Effect_Extension_Era2_Chickasaw_Synergy00.Effects.0.Path.PropertyToFollow=json:["Settlement"]'
  'Effect_Extension_Era2_Chickasaw_Synergy00.Effects.0.ApplyEffectOnSource=0'
  'Effect_Extension_Era2_Chickasaw_Synergy00.startingType=json:"Amplitude.Mercury.Simulation.District, Amplitude.Mercury.Firstpass"'
  'Extension_Era5_Seminole.OwnDescriptorReferences=json:[{"serializableElementName":"Effect_Extension_Era5_Seminole"},{"serializableElementName":"Effect_Extension_Era5_Seminole_Synergy00"},{"serializableElementName":"Tag_Extension_PublicOrderLoss"},{"serializableElementName":"Effect_Extension_Era5_Seminole_Synergy01"}]'
  'NavalUnit_Era3_Song_Junkship.OwnDescriptorReferences=json:[{"serializableElementName":"Effect_NavalUnit_Era3_Song_Junkship"},{"serializableElementName":"Effect_Unit_Era3"},{"serializableElementName":"Effect_Unit_Emblematic"},{"serializableElementName":"Effect_Unit_Era3"},{"serializableElementName":"Tag_Unit_Vehicle"}]'
)
# More Immersive Cultures' Harappan district carries ENCReload's repeatable-only culture effect.
MIC_EFFECT_SETS=('Extension_Era1_HarappanCivilization_02.OwnDescriptorReferences=json:[{"serializableElementName":"Effect_Extension_Base_Food"},{"serializableElementName":"Effect_Extension_Era1_HarappanCivilization_02"},{"serializableElementName":"Effect_Extension_Era1_HarappanCivilization_02_Synergy00"}]')
AMC_PRESENTATION_SETS=(PresentationNavalUnit_Era2_PacificPolynesians_Waakaulua_Dummy.IsWorkInProgress=1)

rm -rf "$OUT" && mkdir -p "$OUT"
"$PY" "$H/zk_modbuilder.py" patch "$AMC" "$OUT" "ZK ENC Compat AMC" "${AMC_SETS[@]/#/--set=}"
"$PY" "$H/zk_modbuilder.py" patch "$SIGMA" "$OUT" "ZK ENC Compat Sigma" "${SIGMA_SETS[@]/#/--set=}"
"$PY" "$H/zk_modbuilder.py" fork "$FIMS" "$OUT" "exploitallFIMS ZK"
"$PY" "$H/zk_modbuilder.py" patch "$EVENTFUL" "$OUT" "ZK Eventful Fix" "${EVENTFUL_EDITS[@]}"
# Alia has no figure at all, so it gets a copy of Sigma's closest Era 1 hull, the Kalinga Boita.
"$PY" "$H/zk_modbuilder.py" patch "$SIGMA" "$OUT" "ZK Sigma Fix" --clone=Era1_Boita_01=Era1_Alia_01 "${SIGMA_PRESENTATION_SETS[@]/#/--set=}" "${SIGMA_EFFECT_SETS[@]/#/--set=}"
"$PY" "$H/zk_modbuilder.py" patch "$SUPER" "$OUT" "ZK Super Pack Fix" "${SUPER_PRESENTATION_SETS[@]/#/--set=}"
"$PY" "$H/zk_modbuilder.py" patch "$MIC" "$OUT" "ZK MIC Fix" "${MIC_EFFECT_SETS[@]/#/--set=}"
"$PY" "$H/zk_modbuilder.py" patch "$AMC" "$OUT" "ZK AMC Fix" "${AMC_PRESENTATION_SETS[@]/#/--set=}"

"$PY" "$H/verify_module.py" "$OUT"/ZK\ ENC\ Compat\ AMC.* "additional mechanic complexity" "${AMC_SETS[@]}"
"$PY" "$H/verify_module.py" "$OUT"/ZK\ ENC\ Compat\ Sigma.* "sigma pack" "${SIGMA_SETS[@]}"
"$PY" "$H/verify_module.py" "$OUT"/exploitallFIMS\ ZK.* "exploitallfims."
"$PY" "$H/verify_module.py" "$OUT"/ZK\ Eventful\ Fix.* "eventful."
"$PY" "$H/verify_module.py" "$OUT"/ZK\ Sigma\ Fix.* "sigma pack" "${SIGMA_PRESENTATION_SETS[@]}" "${SIGMA_EFFECT_SETS[@]}"
"$PY" "$H/verify_module.py" "$OUT"/ZK\ Super\ Pack\ Fix.* "super pack." "${SUPER_PRESENTATION_SETS[@]}"
"$PY" "$H/verify_module.py" "$OUT"/ZK\ MIC\ Fix.* "more immersive cultures." "${MIC_EFFECT_SETS[@]}"
"$PY" "$H/verify_module.py" "$OUT"/ZK\ AMC\ Fix.* "additional mechanic complexity" "${AMC_PRESENTATION_SETS[@]}"

mkdir -p "$INSTALL"
rm -rf "$INSTALL"/ZK\ ENC\ Compat\ AMC.* "$INSTALL"/ZK\ ENC\ Compat\ Sigma.* "$INSTALL"/exploitallFIMS\ ZK.* "$INSTALL"/ZK\ Eventful\ Fix.* \
  "$INSTALL"/ZK\ Sigma\ Fix.* "$INSTALL"/ZK\ Super\ Pack\ Fix.* "$INSTALL"/ZK\ AMC\ Fix.* "$INSTALL"/ZK\ MIC\ Fix.*
cp -r "$OUT"/* "$INSTALL"/
echo "installed: $(ls "$INSTALL" | tr '\n' ' ')"
