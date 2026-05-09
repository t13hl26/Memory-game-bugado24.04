using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MainMenuSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/MainMenu.unity";
    private const string StoryScenePath = "Assets/Scenes/StoryMenu.unity";
    private const string MultiplayerScenePath = "Assets/Scenes/Level3_Multiplayer.unity";
    private static readonly Color Bg = Hex("#6F8E55");
    private static readonly Color Wood = Hex("#E8D0A4");
    private static readonly Color WoodShadow = Hex("#A14828");
    private static readonly Color DarkShadow = Hex("#74261A");
    private static readonly Color Light = Hex("#FFF2D2");
    private static readonly Color Primary = Hex("#F0E0B5");
    private static readonly Color Hover = Hex("#F8EAC7");
    private static readonly Color Pressed = Hex("#E1CC98");
    private static readonly Color TextMain = Hex("#7A2D21");
    private static readonly Color Cream = Hex("#FFF2D2");
    private static readonly Color HeaderGreen = Hex("#6E9253");
    private static readonly Color SoftGlow = new Color(0.95f, 0.91f, 0.80f, 0.08f);
    private static readonly Color SoftShadow = new Color(DarkShadow.r, DarkShadow.g, DarkShadow.b, 0.45f);

    [MenuItem("Tools/Main Menu/Create Or Update Main Menu Scene")]
    public static void CreateOrUpdateScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateCamera();
        CreateEventSystem();

        Canvas canvas = CreateCanvas();
        MainMenuController controller = CreateMenuUI(canvas.transform);
        controller.storySceneName = SceneIds.StoryMenu;
        controller.multiplayerSceneName = SceneIds.Gameplay;

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        UpdateBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (!Application.isBatchMode)
            EditorUtility.DisplayDialog("Main Menu", "Cena principal criada/atualizada em Assets/Scenes/MainMenu.unity", "OK");
    }

    [MenuItem("Tools/Main Menu/Rebuild Menu Scenes")]
    public static void RebuildMenuScenes()
    {
        CreateOrUpdateScene();
        StoryMenuSceneBuilder.CreateOrUpdateScene();
    }

    private static void CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Bg;

        cameraObject.AddComponent<AudioListener>();
    }

    private static void CreateEventSystem()
    {
        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    private static Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject("Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static MainMenuController CreateMenuUI(Transform canvasTransform)
    {
        GameObject root = CreateUIObject("MainMenuRoot", canvasTransform);
        Stretch(root.GetComponent<RectTransform>());

        CreateBackground(root.transform);
        GameObject leftPanel = CreatePanel("LeftPanel", root.transform, Wood);
        AnchorPanel(leftPanel.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.48f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f));
        AddPanelShadow(leftPanel, SoftShadow, new Vector2(8f, -8f));
        AddPanelOutline(leftPanel, WoodShadow, new Vector2(3f, -3f));

        GameObject accentPanel = CreatePanel("AccentPanel", root.transform, Wood);
        AnchorPanel(accentPanel.GetComponent<RectTransform>(), new Vector2(0.52f, 0.08f), new Vector2(0.94f, 0.92f), Vector2.zero, Vector2.zero);
        AddPanelShadow(accentPanel, SoftShadow, new Vector2(8f, -8f));
        AddPanelOutline(accentPanel, WoodShadow, new Vector2(3f, -3f));

        GameObject leftInset = CreatePanel("LeftInset", leftPanel.transform, new Color(1f, 1f, 1f, 0.06f));
        AnchorPanel(leftInset.GetComponent<RectTransform>(), new Vector2(0.08f, 0.09f), new Vector2(0.92f, 0.9f), Vector2.zero, Vector2.zero);

        Text title = CreateText("Title", leftPanel.transform, "MEMORY RIVER", 64, FontStyle.Bold, TextAnchor.MiddleLeft);
        title.color = TextMain;
        SetRect(title.rectTransform, new Vector2(110f, -68f), new Vector2(620f, 90f));

        Text subtitle = CreateText("Subtitle", leftPanel.transform, "Campanha separada e fluxo de sala preparado.", 28, FontStyle.Normal, TextAnchor.UpperLeft);
        subtitle.color = new Color(TextMain.r, TextMain.g, TextMain.b, 0.9f);
        subtitle.horizontalOverflow = HorizontalWrapMode.Wrap;
        subtitle.verticalOverflow = VerticalWrapMode.Truncate;
        SetRect(subtitle.rectTransform, new Vector2(110f, -150f), new Vector2(620f, 80f));

        MainMenuController controller = root.AddComponent<MainMenuController>();

        GameObject mainPanel = CreateUIObject("MainPanel", leftPanel.transform);
        SetRect(mainPanel.GetComponent<RectTransform>(), new Vector2(110f, -270f), new Vector2(620f, 520f));

        GameObject createPanel = CreateUIObject("CreateMatchPanel", leftPanel.transform);
        SetRect(createPanel.GetComponent<RectTransform>(), new Vector2(110f, -270f), new Vector2(620f, 520f));

        GameObject joinPanel = CreateUIObject("JoinMatchPanel", leftPanel.transform);
        SetRect(joinPanel.GetComponent<RectTransform>(), new Vector2(110f, -270f), new Vector2(620f, 520f));

        controller.mainPanel = mainPanel;
        controller.createMatchPanel = createPanel;
        controller.joinMatchPanel = joinPanel;

        mainPanel.SetActive(true);
        createPanel.SetActive(false);
        joinPanel.SetActive(false);

        BuildMainPanel(mainPanel.transform, controller);
        BuildCreatePanel(createPanel.transform, controller);
        BuildJoinPanel(joinPanel.transform, controller);

        Text status = CreateText("StatusText", leftPanel.transform, "Escolha um modo de jogo.", 22, FontStyle.Italic, TextAnchor.MiddleLeft);
        status.color = new Color(TextMain.r, TextMain.g, TextMain.b, 0.85f);
        SetRect(status.rectTransform, new Vector2(110f, -845f), new Vector2(620f, 40f));
        controller.statusText = status;

        CreateDecorativeCard(accentPanel.transform);
        return controller;
    }

    private static void BuildMainPanel(Transform parent, MainMenuController controller)
    {
        Button storyButton = CreateButton(parent, "Modo Historia", "Abrir o mapa da campanha");
        SetRect(storyButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(620f, 110f));
        UnityEventTools.AddPersistentListener(storyButton.onClick, controller.StartStoryMode);

        Button createButton = CreateButton(parent, "Criar Partida", "Crie uma sala online e compartilhe o codigo.");
        SetRect(createButton.GetComponent<RectTransform>(), new Vector2(0f, -134f), new Vector2(620f, 110f));
        UnityEventTools.AddPersistentListener(createButton.onClick, controller.OpenCreateMatchPanel);

        Button joinButton = CreateButton(parent, "Encontrar Partida", "Entrar com codigo quando o multiplayer estiver ativo");
        SetRect(joinButton.GetComponent<RectTransform>(), new Vector2(0f, -268f), new Vector2(620f, 110f));
        UnityEventTools.AddPersistentListener(joinButton.onClick, controller.OpenJoinMatchPanel);

        Button exitButton = CreateButton(parent, "Sair", "Fechar o jogo");
        SetRect(exitButton.GetComponent<RectTransform>(), new Vector2(0f, -402f), new Vector2(620f, 110f));
        UnityEventTools.AddPersistentListener(exitButton.onClick, controller.ExitGame);
    }

    private static void BuildCreatePanel(Transform parent, MainMenuController controller)
    {
        Text header = CreateText("Header", parent, "CRIAR PARTIDA", 40, FontStyle.Bold, TextAnchor.MiddleLeft);
        header.color = TextMain;
        SetRect(header.rectTransform, new Vector2(0f, 0f), new Vector2(620f, 60f));

        Text description = CreateText("Description", parent, "Crie uma sala, compartilhe o codigo e espere o segundo jogador entrar.", 22, FontStyle.Normal, TextAnchor.UpperLeft);
        description.color = new Color(TextMain.r, TextMain.g, TextMain.b, 0.9f);
        description.horizontalOverflow = HorizontalWrapMode.Wrap;
        description.verticalOverflow = VerticalWrapMode.Truncate;
        SetRect(description.rectTransform, new Vector2(0f, -78f), new Vector2(620f, 95f));

        Text roomCode = CreateText("RoomCode", parent, "------", 56, FontStyle.Bold, TextAnchor.MiddleLeft);
        roomCode.color = TextMain;
        SetRect(roomCode.rectTransform, new Vector2(0f, -196f), new Vector2(620f, 70f));
        controller.roomCodeText = roomCode;

        Button startButton = CreateButton(parent, "Criar Sala", "Gera o codigo da partida e abre a sala.");
        SetRect(startButton.GetComponent<RectTransform>(), new Vector2(0f, -300f), new Vector2(390f, 110f));
        UnityEventTools.AddPersistentListener(startButton.onClick, controller.ConfirmCreateMatch);

        Button backButton = CreateButton(parent, "Voltar", "Retornar ao menu");
        SetRect(backButton.GetComponent<RectTransform>(), new Vector2(410f, -300f), new Vector2(210f, 110f));
        UnityEventTools.AddPersistentListener(backButton.onClick, controller.ShowMainPanel);
    }

    private static void BuildJoinPanel(Transform parent, MainMenuController controller)
    {
        Text header = CreateText("Header", parent, "ENCONTRAR PARTIDA", 40, FontStyle.Bold, TextAnchor.MiddleLeft);
        header.color = TextMain;
        SetRect(header.rectTransform, new Vector2(0f, 0f), new Vector2(620f, 60f));

        Text description = CreateText("Description", parent, "Digite o codigo que sera usado quando o multiplayer estiver ativo.", 22, FontStyle.Normal, TextAnchor.UpperLeft);
        description.color = new Color(TextMain.r, TextMain.g, TextMain.b, 0.9f);
        description.horizontalOverflow = HorizontalWrapMode.Wrap;
        description.verticalOverflow = VerticalWrapMode.Truncate;
        SetRect(description.rectTransform, new Vector2(0f, -78f), new Vector2(620f, 95f));

        InputField joinInput = CreateInputField(parent, "Codigo da partida");
        SetRect(joinInput.GetComponent<RectTransform>(), new Vector2(0f, -196f), new Vector2(620f, 70f));
        controller.joinCodeInput = joinInput;

        Button joinButton = CreateButton(parent, "Encontrar Sala", "Use o codigo para entrar na sala existente.");
        SetRect(joinButton.GetComponent<RectTransform>(), new Vector2(0f, -300f), new Vector2(390f, 110f));
        UnityEventTools.AddPersistentListener(joinButton.onClick, controller.ConfirmJoinMatch);

        Button backButton = CreateButton(parent, "Voltar", "Retornar ao menu");
        SetRect(backButton.GetComponent<RectTransform>(), new Vector2(410f, -300f), new Vector2(210f, 110f));
        UnityEventTools.AddPersistentListener(backButton.onClick, controller.ShowMainPanel);
    }

    private static void CreateBackground(Transform parent)
    {
        GameObject bg = CreatePanel("Background", parent, Bg);
        Stretch(bg.GetComponent<RectTransform>());

        GameObject glowTop = CreatePanel("GlowTop", parent, SoftGlow);
        AnchorPanel(glowTop.GetComponent<RectTransform>(), new Vector2(0.44f, 0.72f), new Vector2(1f, 1f), new Vector2(-220f, -40f), new Vector2(-40f, -140f));

        GameObject glowBottom = CreatePanel("GlowBottom", parent, new Color(0.95f, 0.91f, 0.80f, 0.05f));
        AnchorPanel(glowBottom.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(1f, 0.4f), new Vector2(-140f, 80f), new Vector2(-80f, 120f));
    }

    private static void CreateDecorativeCard(Transform parent)
    {
        GameObject cardFrame = CreatePanel("CardFrame", parent, Wood);
        AnchorPanel(cardFrame.GetComponent<RectTransform>(), new Vector2(0.18f, 0.16f), new Vector2(0.82f, 0.84f), Vector2.zero, Vector2.zero);
        AddPanelShadow(cardFrame, SoftShadow, new Vector2(7f, -7f));
        AddPanelOutline(cardFrame, WoodShadow, new Vector2(3f, -3f));

        GameObject header = CreatePanel("CardHeader", parent, HeaderGreen);
        AnchorPanel(header.GetComponent<RectTransform>(), new Vector2(0.17f, 0.8f), new Vector2(0.83f, 0.93f), Vector2.zero, Vector2.zero);
        AddPanelShadow(header, new Color(DarkShadow.r, DarkShadow.g, DarkShadow.b, 0.35f), new Vector2(4f, -4f));
        AddPanelOutline(header, WoodShadow, new Vector2(2f, -2f));

        Text headerText = CreateText("CardHeaderText", header.transform, "COMPLETE", 28, FontStyle.Bold, TextAnchor.MiddleCenter);
        headerText.color = Cream;
        Stretch(headerText.rectTransform);

        GameObject card = CreatePanel("DecorativeCard", cardFrame.transform, Primary);
        AnchorPanel(card.GetComponent<RectTransform>(), new Vector2(0.26f, 0.13f), new Vector2(0.74f, 0.87f), Vector2.zero, Vector2.zero);
        AddPanelShadow(card, new Color(0f, 0f, 0f, 0.08f), new Vector2(4f, -4f));
        AddPanelOutline(card, WoodShadow, new Vector2(2f, -2f));

        GameObject innerCard = CreatePanel("DecorativeCardInner", card.transform, HeaderGreen);
        AnchorPanel(innerCard.GetComponent<RectTransform>(), new Vector2(0.15f, 0.13f), new Vector2(0.85f, 0.87f), Vector2.zero, Vector2.zero);
        AddPanelOutline(innerCard, WoodShadow, new Vector2(2f, -2f));

        Text icon = CreateText("DecorativeCardIcon", card.transform, "?", 96, FontStyle.Bold, TextAnchor.MiddleCenter);
        icon.color = Cream;
        Stretch(icon.rectTransform, new Vector2(0f, 0f), new Vector2(0f, -20f));

        Text caption = CreateText("Caption", parent, "Historia separada e sala privada", 26, FontStyle.Bold, TextAnchor.MiddleCenter);
        caption.color = TextMain;
        AnchorPanel(caption.rectTransform, new Vector2(0.08f, 0.04f), new Vector2(0.92f, 0.16f), Vector2.zero, Vector2.zero);
    }

    private static Button CreateButton(Transform parent, string title, string subtitle)
    {
        Color buttonColor = Primary;
        Color hoverColor = Hover;
        Color pressedColor = Pressed;

        GameObject buttonObject = CreatePanel(title + "Button", parent, buttonColor);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 120f);
        AddPanelShadow(buttonObject, new Color(0f, 0f, 0f, 0.08f), new Vector2(4f, -4f));
        AddPanelOutline(buttonObject, WoodShadow, new Vector2(3f, -3f));

        Image image = buttonObject.GetComponent<Image>();
        image.color = buttonColor;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = buttonColor;
        colors.highlightedColor = hoverColor;
        colors.pressedColor = pressedColor;
        colors.disabledColor = new Color(0.82f, 0.78f, 0.69f, 1f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        Text titleText = CreateText("Title", buttonObject.transform, title, 30, FontStyle.Bold, TextAnchor.UpperLeft);
        titleText.color = TextMain;
        titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
        titleText.verticalOverflow = VerticalWrapMode.Truncate;
        AnchorPanel(titleText.rectTransform, new Vector2(0f, 0.54f), new Vector2(1f, 1f), new Vector2(28f, 0f), new Vector2(-28f, -16f));

        Text subtitleText = CreateText("Subtitle", buttonObject.transform, subtitle, 18, FontStyle.Normal, TextAnchor.LowerLeft);
        subtitleText.color = new Color(TextMain.r, TextMain.g, TextMain.b, 0.85f);
        subtitleText.horizontalOverflow = HorizontalWrapMode.Wrap;
        subtitleText.verticalOverflow = VerticalWrapMode.Truncate;
        AnchorPanel(subtitleText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.44f), new Vector2(28f, 14f), new Vector2(-28f, 0f));

        return button;
    }

    private static InputField CreateInputField(Transform parent, string placeholder)
    {
        GameObject root = CreatePanel("JoinCodeInput", parent, Light);
        Image image = root.GetComponent<Image>();
        image.color = Light;
        AddPanelShadow(root, new Color(0f, 0f, 0f, 0.07f), new Vector2(4f, -4f));
        AddPanelOutline(root, WoodShadow, new Vector2(2f, -2f));

        InputField inputField = root.AddComponent<InputField>();
        inputField.characterLimit = 12;
        inputField.contentType = InputField.ContentType.Alphanumeric;
        inputField.targetGraphic = image;

        GameObject textObject = CreateUIObject("Text", root.transform);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        Stretch(textRect, new Vector2(24f, 16f), new Vector2(-24f, -16f));
        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 30;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = TextMain;
        text.supportRichText = false;

        GameObject placeholderObject = CreateUIObject("Placeholder", root.transform);
        RectTransform placeholderRect = placeholderObject.GetComponent<RectTransform>();
        Stretch(placeholderRect, new Vector2(24f, 16f), new Vector2(-24f, -16f));
        Text placeholderText = placeholderObject.AddComponent<Text>();
        placeholderText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        placeholderText.fontSize = 28;
        placeholderText.alignment = TextAnchor.MiddleLeft;
        placeholderText.color = new Color(TextMain.r, TextMain.g, TextMain.b, 0.55f);
        placeholderText.text = placeholder;

        inputField.textComponent = text;
        inputField.placeholder = placeholderText;
        return inputField;
    }

    private static Text CreateText(string name, Transform parent, string content, int fontSize, FontStyle fontStyle, TextAnchor anchor)
    {
        GameObject textObject = CreateUIObject(name, parent);
        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = anchor;
        text.color = TextMain;
        text.supportRichText = false;
        return text;
    }

    private static GameObject CreatePanel(string name, Transform parent, Color color)
    {
        GameObject panel = CreateUIObject(name, parent);
        Image image = panel.AddComponent<Image>();
        image.color = color;
        return panel;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private static void Stretch(RectTransform rectTransform)
    {
        Stretch(rectTransform, Vector2.zero, Vector2.zero);
    }

    private static void Stretch(RectTransform rectTransform, Vector2 offsetMin, Vector2 offsetMax)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = offsetMin;
        rectTransform.offsetMax = offsetMax;
    }

    private static void AnchorPanel(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = offsetMin;
        rectTransform.offsetMax = offsetMax;
    }

    private static void SetRect(RectTransform rectTransform, Vector2 anchoredPosition, Vector2 size)
    {
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;
    }

    private static void UpdateBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();
        AddSceneIfExists(scenes, ScenePath);
        AddSceneIfExists(scenes, StoryScenePath);
        AddSceneIfExists(scenes, MultiplayerScenePath);
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void AddSceneIfExists(List<EditorBuildSettingsScene> scenes, string path)
    {
        SceneAsset asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        if (asset != null)
            scenes.Add(new EditorBuildSettingsScene(path, true));
    }

    private static void AddPanelShadow(GameObject target, Color color, Vector2 distance)
    {
        Shadow shadow = target.AddComponent<Shadow>();
        shadow.effectColor = color;
        shadow.effectDistance = distance;
        shadow.useGraphicAlpha = true;
    }

    private static void AddPanelOutline(GameObject target, Color color, Vector2 distance)
    {
        Outline outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = true;
    }

    private static Color Hex(string hex)
    {
        Color color;
        ColorUtility.TryParseHtmlString(hex, out color);
        return color;
    }
}
