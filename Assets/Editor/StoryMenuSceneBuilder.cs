using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class StoryMenuSceneBuilder
{
    private const string StoryScenePath = "Assets/Scenes/StoryMenu.unity";
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string GameplayScenePath = "Assets/Scenes/Level3_Multiplayer.unity";
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
    private static readonly Color SoftShadow = new Color(DarkShadow.r, DarkShadow.g, DarkShadow.b, 0.45f);

    [MenuItem("Tools/Main Menu/Create Or Update Story Menu Scene")]
    public static void CreateOrUpdateScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateCamera();
        CreateEventSystem();
        Canvas canvas = CreateCanvas();
        CreateStoryMenu(canvas.transform);

        EditorSceneManager.SaveScene(scene, StoryScenePath);
        EditorSceneManager.OpenScene(StoryScenePath, OpenSceneMode.Single);
        UpdateBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (!Application.isBatchMode)
            EditorUtility.DisplayDialog("Story Menu", "Cena de historia criada/atualizada em Assets/Scenes/StoryMenu.unity", "OK");
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

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static void CreateStoryMenu(Transform canvasTransform)
    {
        GameObject root = CreateUIObject("StoryMenuRoot", canvasTransform);
        Stretch(root.GetComponent<RectTransform>());

        GameObject background = CreatePanel("Background", root.transform, Bg);
        Stretch(background.GetComponent<RectTransform>());

        GameObject leftPanel = CreatePanel("LeftPanel", root.transform, Wood);
        Anchor(leftPanel.GetComponent<RectTransform>(), new Vector2(0.04f, 0.08f), new Vector2(0.47f, 0.92f));
        AddPanelShadow(leftPanel, SoftShadow, new Vector2(8f, -8f));
        AddPanelOutline(leftPanel, WoodShadow, new Vector2(3f, -3f));

        GameObject rightPanel = CreatePanel("AccentPanel", root.transform, Wood);
        Anchor(rightPanel.GetComponent<RectTransform>(), new Vector2(0.52f, 0.08f), new Vector2(0.95f, 0.92f));
        AddPanelShadow(rightPanel, SoftShadow, new Vector2(8f, -8f));
        AddPanelOutline(rightPanel, WoodShadow, new Vector2(3f, -3f));

        GameObject leftInset = CreatePanel("LeftInset", leftPanel.transform, new Color(1f, 1f, 1f, 0.06f));
        AnchorPanel(leftInset.GetComponent<RectTransform>(), new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.9f), Vector2.zero, Vector2.zero);

        StoryMenuController controller = root.AddComponent<StoryMenuController>();

        Text title = CreateText("Title", leftPanel.transform, "MODO HISTORIA", 60, FontStyle.Bold, TextAnchor.UpperLeft);
        title.color = TextMain;
        SetRect(title.rectTransform, new Vector2(60f, -48f), new Vector2(560f, 78f));

        Text subtitle = CreateText("Subtitle", leftPanel.transform, "Campanha em capitulos usando a mesma base do tabuleiro.", 24, FontStyle.Normal, TextAnchor.UpperLeft);
        subtitle.color = new Color(TextMain.r, TextMain.g, TextMain.b, 0.9f);
        subtitle.horizontalOverflow = HorizontalWrapMode.Wrap;
        subtitle.verticalOverflow = VerticalWrapMode.Truncate;
        SetRect(subtitle.rectTransform, new Vector2(60f, -118f), new Vector2(560f, 60f));

        Text chapterTitle = CreateText("ChapterTitle", leftPanel.transform, "Capitulo 1", 42, FontStyle.Bold, TextAnchor.UpperLeft);
        chapterTitle.color = TextMain;
        SetRect(chapterTitle.rectTransform, new Vector2(60f, -210f), new Vector2(500f, 58f));
        controller.chapterTitleText = chapterTitle;

        Text chapterDescription = CreateText("ChapterDescription", leftPanel.transform, "Memorize as cartas, limpe o tabuleiro e avance na campanha.", 24, FontStyle.Normal, TextAnchor.UpperLeft);
        chapterDescription.color = new Color(TextMain.r, TextMain.g, TextMain.b, 0.9f);
        chapterDescription.horizontalOverflow = HorizontalWrapMode.Wrap;
        chapterDescription.verticalOverflow = VerticalWrapMode.Truncate;
        SetRect(chapterDescription.rectTransform, new Vector2(60f, -278f), new Vector2(560f, 96f));
        controller.chapterDescriptionText = chapterDescription;

        Text progress = CreateText("Progress", leftPanel.transform, "Fluxo de historia separado do multiplayer.", 22, FontStyle.Italic, TextAnchor.MiddleLeft);
        progress.color = new Color(TextMain.r, TextMain.g, TextMain.b, 0.85f);
        SetRect(progress.rectTransform, new Vector2(60f, -392f), new Vector2(560f, 36f));
        controller.progressText = progress;

        Button startButton = CreateButton(leftPanel.transform, "Jogar Capitulo 1", "Entrar na campanha");
        SetRect(startButton.GetComponent<RectTransform>(), new Vector2(60f, -458f), new Vector2(560f, 112f));
        UnityEventTools.AddPersistentListener(startButton.onClick, controller.StartSelectedPhase);

        Button backButton = CreateButton(leftPanel.transform, "Voltar", "Retornar ao menu principal");
        SetRect(backButton.GetComponent<RectTransform>(), new Vector2(60f, -592f), new Vector2(360f, 96f));
        UnityEventTools.AddPersistentListener(backButton.onClick, controller.BackToMainMenu);

        Text status = CreateText("Status", leftPanel.transform, "Comece pelo Capitulo 1.", 20, FontStyle.Normal, TextAnchor.MiddleLeft);
        status.color = new Color(TextMain.r, TextMain.g, TextMain.b, 0.85f);
        SetRect(status.rectTransform, new Vector2(60f, -715f), new Vector2(560f, 36f));
        controller.statusText = status;

        CreateStoryPreview(rightPanel.transform);
    }

    private static void CreateStoryPreview(Transform parent)
    {
        Text caption = CreateText("Caption", parent, "Campanha", 48, FontStyle.Bold, TextAnchor.UpperCenter);
        caption.color = TextMain;
        SetRectCenterTop(caption.rectTransform, 0f, -46f, 440f, 70f);

        GameObject frame = CreatePanel("CardFrame", parent, Wood);
        Anchor(frame.GetComponent<RectTransform>(), new Vector2(0.1f, 0.2f), new Vector2(0.9f, 0.74f));
        AddPanelShadow(frame, SoftShadow, new Vector2(7f, -7f));
        AddPanelOutline(frame, WoodShadow, new Vector2(3f, -3f));

        for (int i = 0; i < 3; i++)
        {
            GameObject card = CreatePanel("CardPreview_" + i, frame.transform, Primary);
            RectTransform rect = card.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.12f + (i * 0.26f), 0.2f);
            rect.anchorMax = new Vector2(0.32f + (i * 0.26f), 0.8f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            AddPanelShadow(card, new Color(0f, 0f, 0f, 0.08f), new Vector2(4f, -4f));
            AddPanelOutline(card, WoodShadow, new Vector2(2f, -2f));

            GameObject innerCard = CreatePanel("Inner_" + i, card.transform, HeaderGreen);
            AnchorPanel(innerCard.GetComponent<RectTransform>(), new Vector2(0.12f, 0.1f), new Vector2(0.88f, 0.9f), Vector2.zero, Vector2.zero);
            AddPanelOutline(innerCard, WoodShadow, new Vector2(2f, -2f));

            Text icon = CreateText("Icon_" + i, card.transform, "?", 72, FontStyle.Bold, TextAnchor.MiddleCenter);
            icon.color = Cream;
            Stretch(icon.rectTransform, new Vector2(0f, 0f), new Vector2(0f, -14f));
        }

        Text note = CreateText("Note", parent, "A campanha usa a mesma base do tabuleiro e evolui por capitulos.", 24, FontStyle.Normal, TextAnchor.MiddleCenter);
        note.color = TextMain;
        note.horizontalOverflow = HorizontalWrapMode.Wrap;
        note.verticalOverflow = VerticalWrapMode.Truncate;
        Anchor(note.rectTransform, new Vector2(0.08f, 0.04f), new Vector2(0.92f, 0.17f));
    }

    private static Button CreateButton(Transform parent, string title, string subtitle)
    {
        Color buttonColor = Primary;
        Color hoverColor = Hover;
        Color pressedColor = Pressed;

        GameObject buttonObject = CreatePanel(title + "Button", parent, buttonColor);
        Image image = buttonObject.GetComponent<Image>();
        image.color = buttonColor;
        AddPanelShadow(buttonObject, new Color(0f, 0f, 0f, 0.08f), new Vector2(4f, -4f));
        AddPanelOutline(buttonObject, WoodShadow, new Vector2(3f, -3f));

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
        AnchorPanel(titleText.rectTransform, new Vector2(0f, 0.54f), new Vector2(1f, 1f), new Vector2(24f, 0f), new Vector2(-24f, -16f));

        Text subtitleText = CreateText("Subtitle", buttonObject.transform, subtitle, 18, FontStyle.Normal, TextAnchor.LowerLeft);
        subtitleText.color = new Color(TextMain.r, TextMain.g, TextMain.b, 0.85f);
        subtitleText.horizontalOverflow = HorizontalWrapMode.Wrap;
        subtitleText.verticalOverflow = VerticalWrapMode.Truncate;
        AnchorPanel(subtitleText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.44f), new Vector2(24f, 14f), new Vector2(-24f, 0f));

        return button;
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

    private static void Anchor(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax)
    {
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
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

    private static void SetRectCenterTop(RectTransform rectTransform, float x, float y, float width, float height)
    {
        rectTransform.anchorMin = new Vector2(0.5f, 1f);
        rectTransform.anchorMax = new Vector2(0.5f, 1f);
        rectTransform.pivot = new Vector2(0.5f, 1f);
        rectTransform.anchoredPosition = new Vector2(x, y);
        rectTransform.sizeDelta = new Vector2(width, height);
    }

    private static void UpdateBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();
        AddSceneIfExists(scenes, MainMenuScenePath);
        AddSceneIfExists(scenes, StoryScenePath);
        AddSceneIfExists(scenes, GameplayScenePath);
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
