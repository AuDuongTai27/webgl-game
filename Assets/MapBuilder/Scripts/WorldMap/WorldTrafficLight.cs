using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldTrafficLight : MonoBehaviour
{
    public enum LightState
    {
        Green = 0,
        Yellow = 1,
        Red = 2
    }

    [Header("Current State")]
    [SerializeField] private LightState currentState = LightState.Green;
    [SerializeField] private float timeRemaining = 10.0f;

    [Header("Durations (Seconds)")]
    public float greenDuration = 10.0f;
    public float yellowDuration = 3.0f;
    public float redDuration = 10.0f;

    [Header("Renderers")]
    [SerializeField] private List<Renderer> redLampRenderers = new List<Renderer>();
    [SerializeField] private List<Renderer> yellowLampRenderers = new List<Renderer>();
    [SerializeField] private List<Renderer> greenLampRenderers = new List<Renderer>();
    [SerializeField] private Light spotOrPointLight;

    // Materials
    private Material matRedActive;
    private Material matRedDim;
    private Material matYellowActive;
    private Material matYellowDim;
    private Material matGreenActive;
    private Material matGreenDim;

    public LightState CurrentLightState => currentState;
    public float TimeRemaining => timeRemaining;

    private void Awake()
    {
        InitializeMaterials();

        // If renderers are not assigned, build highly realistic 3D model
        if (redLampRenderers.Count == 0 || yellowLampRenderers.Count == 0 || greenLampRenderers.Count == 0)
        {
            BuildRealistic3DModel();
        }
    }

    private void Start()
    {
        SetState(currentState);
    }

    private void Update()
    {
        timeRemaining -= Time.deltaTime;

        if (timeRemaining <= 0f)
        {
            AdvanceState();
        }
    }

    private void AdvanceState()
    {
        switch (currentState)
        {
            case LightState.Green:
                SetState(LightState.Yellow);
                break;
            case LightState.Yellow:
                SetState(LightState.Red);
                break;
            case LightState.Red:
                SetState(LightState.Green);
                break;
        }
    }

    public void SetState(LightState newState)
    {
        currentState = newState;

        switch (currentState)
        {
            case LightState.Green:
                timeRemaining = greenDuration;
                ApplyLampMaterials(redActive: false, yellowActive: false, greenActive: true);
                if (spotOrPointLight != null)
                {
                    spotOrPointLight.color = new Color(0.1f, 1.0f, 0.3f);
                    spotOrPointLight.enabled = true;
                }
                break;

            case LightState.Yellow:
                timeRemaining = yellowDuration;
                ApplyLampMaterials(redActive: false, yellowActive: true, greenActive: false);
                if (spotOrPointLight != null)
                {
                    spotOrPointLight.color = new Color(1.0f, 0.85f, 0.05f);
                    spotOrPointLight.enabled = true;
                }
                break;

            case LightState.Red:
                timeRemaining = redDuration;
                ApplyLampMaterials(redActive: true, yellowActive: false, greenActive: false);
                if (spotOrPointLight != null)
                {
                    spotOrPointLight.color = new Color(1.0f, 0.1f, 0.1f);
                    spotOrPointLight.enabled = true;
                }
                break;
        }
    }

    private void ApplyLampMaterials(bool redActive, bool yellowActive, bool greenActive)
    {
        Material targetRed = redActive ? matRedActive : matRedDim;
        Material targetYellow = yellowActive ? matYellowActive : matYellowDim;
        Material targetGreen = greenActive ? matGreenActive : matGreenDim;

        for (int i = 0; i < redLampRenderers.Count; i++)
        {
            if (redLampRenderers[i] != null) redLampRenderers[i].sharedMaterial = targetRed;
        }
        for (int i = 0; i < yellowLampRenderers.Count; i++)
        {
            if (yellowLampRenderers[i] != null) yellowLampRenderers[i].sharedMaterial = targetYellow;
        }
        for (int i = 0; i < greenLampRenderers.Count; i++)
        {
            if (greenLampRenderers[i] != null) greenLampRenderers[i].sharedMaterial = targetGreen;
        }
    }

    private void InitializeMaterials()
    {
        // Prioritize Unlit/Color so it's ALWAYS 100% vibrant, never affected by dark lighting or missing ambient maps
        Shader unlitShader = Shader.Find("Unlit/Color")
            ?? Shader.Find("Mobile/Unlit (Supports Lightmap)")
            ?? Shader.Find("Sprites/Default")
            ?? Shader.Find("Standard");

        // Vibrant glowing active colors
        matRedActive = CreateSolidMaterial(unlitShader, new Color(1.0f, 0.05f, 0.05f));
        matYellowActive = CreateSolidMaterial(unlitShader, new Color(1.0f, 0.85f, 0.0f));
        matGreenActive = CreateSolidMaterial(unlitShader, new Color(0.0f, 1.0f, 0.25f));

        // Dark inactive lens colors
        matRedDim = CreateSolidMaterial(unlitShader, new Color(0.35f, 0.06f, 0.06f));
        matYellowDim = CreateSolidMaterial(unlitShader, new Color(0.35f, 0.28f, 0.05f));
        matGreenDim = CreateSolidMaterial(unlitShader, new Color(0.05f, 0.32f, 0.10f));
    }

    private Material CreateSolidMaterial(Shader shader, Color color)
    {
        Material mat = new Material(shader);
        mat.color = color;
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 1.5f);
        }
        return mat;
    }

    /// <summary>
    /// Builds a highly realistic 3D traffic light with:
    /// - Heavy concrete/metal base plate
    /// - Metallic steel pole
    /// - Signal housing with backplate & yellow reflective border
    /// - Front & Back 3D lamps with visors (never appears black from any angle!)
    /// - Real-time point light for night/day glow
    /// </summary>
    private void BuildRealistic3DModel()
    {
        Transform existingHousing = transform.Find("TrafficLight_Housing");
        if (existingHousing != null)
        {
            return;
        }

        Shader unlitShader = Shader.Find("Unlit/Color") ?? Shader.Find("Mobile/Unlit (Supports Lightmap)") ?? Shader.Find("Standard");

        Material poleMat = CreateSolidMaterial(unlitShader, new Color(0.38f, 0.42f, 0.46f));      // Steel silver/grey
        Material housingMat = CreateSolidMaterial(unlitShader, new Color(0.12f, 0.13f, 0.15f));   // Matte dark charcoal
        Material yellowBorderMat = CreateSolidMaterial(unlitShader, new Color(0.96f, 0.72f, 0.12f)); // Traffic reflective yellow
        Material visorMat = CreateSolidMaterial(unlitShader, new Color(0.08f, 0.09f, 0.10f));     // Deep black visor hood

        // 1. Base Plate (Ground flange)
        GameObject basePlate = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        basePlate.name = "Pole_BasePlate";
        basePlate.transform.SetParent(transform, false);
        basePlate.transform.localPosition = new Vector3(0, 0.08f, 0);
        basePlate.transform.localScale = new Vector3(0.55f, 0.08f, 0.55f);
        Renderer baseRen = basePlate.GetComponent<Renderer>();
        if (baseRen != null) baseRen.sharedMaterial = poleMat;

        // 2. Vertical Pole
        GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.name = "Pole_Body";
        pole.transform.SetParent(transform, false);
        pole.transform.localPosition = new Vector3(0, 1.45f, 0);
        pole.transform.localScale = new Vector3(0.14f, 1.45f, 0.14f);
        Renderer poleRen = pole.GetComponent<Renderer>();
        if (poleRen != null) poleRen.sharedMaterial = poleMat;

        // 3. Mounting Bracket (Connecting pole to housing)
        GameObject bracket = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bracket.name = "Mount_Bracket";
        bracket.transform.SetParent(transform, false);
        bracket.transform.localPosition = new Vector3(0, 3.2f, 0);
        bracket.transform.localScale = new Vector3(0.25f, 0.12f, 0.55f);
        Renderer bracketRen = bracket.GetComponent<Renderer>();
        if (bracketRen != null) bracketRen.sharedMaterial = poleMat;

        // 4. Housing Parent Root
        GameObject housingRoot = new GameObject("TrafficLight_Housing");
        housingRoot.transform.SetParent(transform, false);
        housingRoot.transform.localPosition = new Vector3(0, 3.2f, 0);

        // 4a. Yellow Reflective Backplate Border
        GameObject yellowBorder = GameObject.CreatePrimitive(PrimitiveType.Cube);
        yellowBorder.name = "Backplate_YellowBorder";
        yellowBorder.transform.SetParent(housingRoot.transform, false);
        yellowBorder.transform.localPosition = Vector3.zero;
        yellowBorder.transform.localScale = new Vector3(0.72f, 1.62f, 0.40f);
        Renderer borderRen = yellowBorder.GetComponent<Renderer>();
        if (borderRen != null) borderRen.sharedMaterial = yellowBorderMat;

        // 4b. Black Backplate Shield
        GameObject blackShield = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blackShield.name = "Backplate_BlackShield";
        blackShield.transform.SetParent(housingRoot.transform, false);
        blackShield.transform.localPosition = Vector3.zero;
        blackShield.transform.localScale = new Vector3(0.64f, 1.54f, 0.42f);
        Renderer shieldRen = blackShield.GetComponent<Renderer>();
        if (shieldRen != null) shieldRen.sharedMaterial = housingMat;

        // 4c. Main Box Housing
        GameObject mainBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mainBox.name = "Housing_Box";
        mainBox.transform.SetParent(housingRoot.transform, false);
        mainBox.transform.localPosition = Vector3.zero;
        mainBox.transform.localScale = new Vector3(0.52f, 1.42f, 0.46f);
        Renderer boxRen = mainBox.GetComponent<Renderer>();
        if (boxRen != null) boxRen.sharedMaterial = housingMat;

        // 5. Lamps on BOTH Front (+Z) and Back (-Z)
        // This ensures the lights are ALWAYS clearly visible, even if the player or camera views it from any angle!
        float[] yPositions = new float[] { 0.44f, 0.0f, -0.44f }; // Top=Red, Mid=Yellow, Bot=Green
        float[] zDirections = new float[] { 1.0f, -1.0f };         // +Z (Front), -Z (Back)

        redLampRenderers.Clear();
        yellowLampRenderers.Clear();
        greenLampRenderers.Clear();

        foreach (float zDir in zDirections)
        {
            string sideName = zDir > 0 ? "Front" : "Back";

            // Red Lamp
            Renderer redRen = CreateLampAssembly(housingRoot.transform, $"RedLamp_{sideName}", yPositions[0], zDir, visorMat);
            redLampRenderers.Add(redRen);

            // Yellow Lamp
            Renderer yellowRen = CreateLampAssembly(housingRoot.transform, $"YellowLamp_{sideName}", yPositions[1], zDir, visorMat);
            yellowLampRenderers.Add(yellowRen);

            // Green Lamp
            Renderer greenRen = CreateLampAssembly(housingRoot.transform, $"GreenLamp_{sideName}", yPositions[2], zDir, visorMat);
            greenLampRenderers.Add(greenRen);
        }

        // 6. Real-time Point Light
        GameObject lightObj = new GameObject("Light_Glow");
        lightObj.transform.SetParent(housingRoot.transform, false);
        lightObj.transform.localPosition = new Vector3(0, 0, 0);
        spotOrPointLight = lightObj.AddComponent<Light>();
        spotOrPointLight.type = LightType.Point;
        spotOrPointLight.range = 8.0f;
        spotOrPointLight.intensity = 2.0f;
    }

    private Renderer CreateLampAssembly(Transform parent, string lampName, float yPos, float zDir, Material visorMat)
    {
        // Circular lens (protruding sphere)
        GameObject lampObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        lampObj.name = lampName;
        lampObj.transform.SetParent(parent, false);
        lampObj.transform.localPosition = new Vector3(0, yPos, zDir * 0.25f);
        lampObj.transform.localScale = new Vector3(0.32f, 0.32f, 0.16f);

        // Sun Visor / Hood over the lamp
        GameObject visor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visor.name = $"{lampName}_Visor";
        visor.transform.SetParent(parent, false);
        visor.transform.localPosition = new Vector3(0, yPos + 0.15f, zDir * 0.26f);
        visor.transform.localRotation = Quaternion.Euler(zDir > 0 ? 30f : -30f, 0, 90f);
        visor.transform.localScale = new Vector3(0.32f, 0.02f, 0.18f);
        Renderer visorRen = visor.GetComponent<Renderer>();
        if (visorRen != null) visorRen.sharedMaterial = visorMat;

        // Remove colliders on lamps to prevent physics conflicts with cars
        Collider col = lampObj.GetComponent<Collider>();
        if (col != null) Destroy(col);
        Collider visorCol = visor.GetComponent<Collider>();
        if (visorCol != null) Destroy(visorCol);

        return lampObj.GetComponent<Renderer>();
    }
}
