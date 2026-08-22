using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UnderwaterGliderTwin.UI
{
    [Serializable]
    public abstract class UiReferenceGroupBase : IUiReferenceGroup
    {
        public void CollectReferenceIssues(Component owner, string prefabName, string groupPath, List<UiReferenceIssue> issues)
        {
            UiReferenceValidator.RequireFields(this, owner, prefabName, groupPath, issues);
        }
    }

    [Serializable]
    public sealed class RuntimeUiReferences : UiReferenceGroupBase
    {
        public ResponsiveLayoutRefs layout = new ResponsiveLayoutRefs();
        public DashboardPanelRefs dashboard = new DashboardPanelRefs();
        public StatusPanelRefs status = new StatusPanelRefs();
        public DataInputPanelRefs dataInput = new DataInputPanelRefs();
        public PlaybackControlsRefs playback = new PlaybackControlsRefs();
        public OceanToolbarRefs oceanToolbar = new OceanToolbarRefs();
    }

    [Serializable]
    public sealed class ResponsiveLayoutRefs : UiReferenceGroupBase
    {
        public RectTransform drawerEntryLayer;
        public Button telemetryDrawerToggle;
        public Button statusDrawerToggle;
        public RectTransform systemBar;
        public RectTransform configurationArea;
        public RectTransform mainBody;
        public RectTransform telemetryColumn;
        public RectTransform viewportColumn;
        public RectTransform statusColumn;
        public RectTransform playbackBar;
        public Image drawerScrim;
    }

    [Serializable]
    public sealed class DashboardPanelRefs : UiReferenceGroupBase
    {
        public RectTransform panel;
        public Button detailsButton;
        [OptionalUiReference] public GameObject navigationReferenceCard;
        public Text depthValue;
        public Text headingValue;
        public Text pitchValue;
        public Text rollValue;
        public Text yawValue;
        public Text batteryValue;
        public Text latitudeValue;
        public Text longitudeValue;
        public Text velocityXValue;
        public Text velocityYValue;
        public Text velocityZValue;
        public Text speedValue;
        public Text verticalSpeedValue;
        public Text horizontalSpeedValue;
        public Text missionTimeValue;
        public Text distanceValue;
        // Optional because prediction diagnostics are absent when prediction is disabled.
        [OptionalUiReference] public Text predictionErrorValue;
        public Text oceanCurrentValue;
        public Text waterSpeedValue;
        public Text groundSpeedValue;
        public Text sideSlipValue;
        public Text netBuoyancyValue;
        public Text energyValue;
        public Text angleOfAttackValue;
        public Text liftForceValue;
        public Text dragForceValue;
        public Text angularRateValue;
        public Text hydrodynamicMomentValue;
        public Text inertiaValue;
        public Text pistonPositionValue;
        public Text controlSurfaceValue;
        public Text actuatorPowerValue;
        public Text dynamicsSummaryValue;
        public RectTransform advancedRowsRoot;
    }

    [Serializable]
    public sealed class StatusPanelRefs : UiReferenceGroupBase
    {
        public RectTransform panel;
        public Image alarmBackground;
        public Image progressFill;
        public Text missionValue;
        public Text modeValue;
        public Text stateValue;
        public Text segmentValue;
        public Text remainingDistanceValue;
        public Text etaValue;
        public Text predictionStatusValue;
        public Text batteryValue;
        public Text driftValue;
        public Text rmseValue;
        public Text maeValue;
        // Optional because confidence is only meaningful for predictor-backed runs.
        [OptionalUiReference] public Text confidenceValue;
        public Text predictionTimeValue;
        public Text engineeringValidationValue;
        public Text alarmValue;
        public Text missionHealthValue;
        public RectTransform predictionMetricRowsRoot;
        public RectTransform predictionMetricRowTemplate;
    }

    [Serializable]
    public sealed class PlaybackControlsRefs : UiReferenceGroupBase
    {
        public RectTransform panel;
        public Button playPauseButton;
        public Button reverseButton;
        public Button replayButton;
        public Button resetButton;
        public Button exportButton;
        public Button exitButton;
        public Button cameraFollowButton;
        public Button cameraGlobalButton;
        public Button cameraOrbitButton;
        public Button missionVolumeButton;
        public Toggle fogToggle;
        public Toggle particlesToggle;
        public Toggle trajectoryToggle;
        public Button speed05Button;
        public Button speed1Button;
        public Button speed2Button;
        public Button speed5Button;
        public Button speed10Button;
        public Slider progressSlider;
        public Text statusText;
    }

    [Serializable]
    public sealed class OceanToolbarRefs : UiReferenceGroupBase
    {
        public RectTransform viewportFrame;
        public RectTransform panel;
        public Text visibleArrowCount;
        public Button cameraFollowCommand;
        public Button cameraGlobalCommand;
        public Button cameraTopCommand;
        public Button cameraSideCommand;
        public Button cameraOrbitCommand;
        public Button cameraResetCommand;
    }

    [Serializable]
    public sealed class DataInputPanelRefs : UiReferenceGroupBase
    {
        public RectTransform panel;
        public Text titleText;
        public Text statusText;
        public RectTransform configurationPanel;
        public MissionSectionRefs mission = new MissionSectionRefs();
        public PredictionSectionRefs prediction = new PredictionSectionRefs();
        public SimulationSectionRefs simulation = new SimulationSectionRefs();
        public OceanSectionRefs ocean = new OceanSectionRefs();
        public FlightLegSectionRefs flightLeg = new FlightLegSectionRefs();
        public DynamicsSectionRefs dynamics = new DynamicsSectionRefs();
    }

    [Serializable]
    public sealed class MissionSectionRefs : UiReferenceGroupBase
    {
        public Text csvSourceLabel;
        public InputField csvPathInput;
        public Button loadCsvButton;
        public InputField missionLongitudeInput;
        public InputField missionLatitudeInput;
    }

    [Serializable]
    public sealed class PredictionSectionRefs : UiReferenceGroupBase
    {
        public Text modelLabel;
        [OptionalUiReference] public RectTransform modelButtonsRoot;
        [OptionalUiReference] public Button xgBoostModelButton;
        // Optional because the prediction section can be removed from a shipped build.
        [OptionalUiReference] public InputField predictionHorizonInput;
        [OptionalUiReference] public Button applyPredictionConfigButton;
        [OptionalUiReference] public Button predictionToggleButton;
        [OptionalUiReference] public Text predictionRuntimeLabel;
    }

    [Serializable]
    public sealed class SimulationSectionRefs : UiReferenceGroupBase
    {
        public InputField cyclesInput;
        public InputField durationInput;
        public InputField targetDepthInput;
        public InputField waterColumnDepthInput;
        public Text referenceCycleDurationValue;
        public Button applyReferenceCycleButton;
        public InputField headingInput;
        public InputField headingDeltaInput;
        public InputField pitchInput;
        public InputField rollInput;
        public Button applyButton;
        public Button flightLegSettingsButton;
    }

    [Serializable]
    public sealed class OceanSectionRefs : UiReferenceGroupBase
    {
        public InputField minDepthInput;
        public InputField maxDepthInput;
        public InputField eastwardInput;
        public InputField northwardInput;
        public Button previousLayerButton;
        public Button nextLayerButton;
        public Button addLayerButton;
        public Button saveLayerButton;
        public Button deleteLayerButton;
        public Button lookupButton;
        public Text layerSummaryText;
        public Button drawerButton;
        public RectTransform oceanCurrentDrawer;
        public Text drawerSummaryText;
        public Text qualitySummaryText;
        public InputField drawerMinDepthInput;
        public InputField drawerMaxDepthInput;
        public InputField drawerEastwardInput;
        public InputField drawerNorthwardInput;
        public InputField prefetchHalfWidthInput;
        public InputField forecastWindowInput;
        public Text fieldSummaryText;
        public Button onlineModeButton;
        public Button cacheOnlyModeButton;
        public Button localFileModeButton;
        public Text acquisitionModeText;
        public InputField localFileInput;
        public Text actualSourceText;
        public Button drawerPreviousButton;
        public Button drawerNextButton;
        public Button drawerAddButton;
        public Button drawerDeleteButton;
        public Button drawerSaveButton;
        public Button drawerLookupButton;
        public Text drawerStatusText;
        public RectTransform dynamicRowsRoot;
        public RectTransform oceanLayerRowTemplate;
    }

    [Serializable]
    public sealed class FlightLegSectionRefs : UiReferenceGroupBase
    {
        public RectTransform drawer;
        public Button closeButton;
        public Button restoreDefaultsButton;
        public InputField descentNetBuoyancyInput;
        public InputField descentPitchInput;
        public InputField descentRollInput;
        public InputField ascentNetBuoyancyInput;
        public InputField ascentPitchInput;
        public InputField ascentRollInput;
        public Text statusText;
    }

    [Serializable]
    public sealed class DynamicsSectionRefs : UiReferenceGroupBase
    {
        public Button seaTrialPresetButton;
        public Button calmWaterPresetButton;
        public Button calibrateFromCsvButton;
        public InputField massInput;
        public InputField referenceAreaInput;
        public InputField referenceLengthInput;
        public InputField wingSpanInput;
        public InputField meanChordInput;
        public InputField rollInertiaInput;
        public InputField pitchInertiaInput;
        public InputField yawInertiaInput;
        public InputField liftSlopeInput;
        public InputField baseDragInput;
        public InputField turnaroundDurationInput;
        public InputField buoyancyExponentInput;
        public InputField buoyancyDeadbandInput;
        public InputField pistonHysteresisInput;
        public InputField rollExponentInput;
        public InputField rollDeadbandInput;
        public InputField rollRestoringGainInput;
        public InputField maxRollMomentInput;
    }
}
