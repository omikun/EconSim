#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Reflection;

public class EditorFontSize : EditorWindow
{
    // enable resize on launch to set a default font size , using this option will disable the ability to have the window accassible 
    // from the context menu ( Window > Editor Font Size ) - bc this is a hacky way to enforce default global font size on application 
    // launch , on script assembly reload and on application enter / exit play mode 

    public static bool RESIZE_ON_LAUNCH = true;
    public static int DEFAULT_GLOBAL_FONT_SIZE = 14;

    [InitializeOnLoadMethod] static void DefaultSize()
    {
        if( ! RESIZE_ON_LAUNCH || DEFAULT_GLOBAL_FONT_SIZE <= 10 ) return;
        var w = GetWindow<EditorFontSize>();
        w.GUICallback = () => {
            w.Resize( DEFAULT_GLOBAL_FONT_SIZE - 10 ) ;
            w.Close();
        };
    }

    [MenuItem("Window/Editor Font Size")]
    static void Open()
    {
        if( RESIZE_ON_LAUNCH ) return;

        GetWindow<EditorFontSize>("Editor Font Size").minSize = new Vector2(180, 30);
    }

    Dictionary<string, bool> foldouts;

    bool Header(string s)
    {
        if (foldouts == null) foldouts = new Dictionary<string, bool>();
        if (!foldouts.ContainsKey(s)) foldouts.Add(s, true);
        GUILayout.Space(5);
        var foldout = EditorGUILayout.Foldout(!foldouts[s], s, true);
        foldouts[s] = !foldout;
        return foldout;
    }

    private void OnDisable()
    {
        guiSkins = null;
        editorStyles = null;
        propFontSizevalidity?.Clear();
        propFontSizevalidity = null;
    }

    static List<GUIStyle> styles;

    PropertyInfo[] editorStyles;
    PropertyInfo[] guiSkins;

    void GrabProperties()
    {
        if (editorStyles == null || editorStyles.Length < 1)
        {
            var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.GetProperty;
            editorStyles = typeof(EditorStyles).GetProperties(flags);
        }
        if (guiSkins == null || guiSkins.Length < 1)
        {
            guiSkins = GUI.skin.GetType().GetProperties();
        }
    }

    Vector2 scroll;

    System.Action GUICallback;

    private void OnGUI()
    {
        rowCount = -1;

        GrabProperties();

        InitStyles();

        var delta = FontSizeRow("Global Zoom", EditorStyles.miniLabel.fontSize.ToString());
        if (delta != 0 && EditorStyles.miniLabel.fontSize + delta > 0)
        {
            Resize( delta );
        }   

        GUILayout.Label("", GUI.skin.horizontalSlider);

        using (var scope = new GUILayout.ScrollViewScope(scroll))
        {
            scroll = scope.scrollPosition;

            if (Header("Editor Styles"))
                foreach (var x in editorStyles)
                    ModifyProp(x, null);

            if (Header("GUI skins"))
                foreach (var x in guiSkins)
                    ModifyProp(x, GUI.skin);

            if (Header("Custom Styles"))
                foreach (var x in GUI.skin.customStyles)
                {
                    FontSizeRow(x);
                    RepaintAll();
                }
        }

        if( GUICallback != null ) GUICallback.Invoke();
    }

    void Resize( int delta )
    {
        // Keep watch for duplicates ( Prevent double modifications )
        styles = new List<GUIStyle>();

        foreach (var x in editorStyles)
        {
            if (!ValidFontProp(x, null)) continue;
            var s = (GUIStyle)x.GetValue(null, null);
            if (styles.Contains(s)) continue;
            styles.Add(s);
            s.fontSize += delta;
        }

        foreach (var x in guiSkins)
        {
            if (!ValidFontProp(x, GUI.skin)) continue;
            var s = (GUIStyle)x.GetValue(GUI.skin, null);
            if (styles.Contains(s)) continue;
            styles.Add(s);
            s.fontSize += delta;
        }
        foreach (var x in GUI.skin.customStyles)
        {
            if (!ValidFontStyle(x) || styles.Contains(x)) continue;
            FixZeroSize(x);
            styles.Add(x);
            x.fontSize += delta;
        }

        styles.Clear();

        RepaintAll();
    }

    void RepaintAll() { foreach (var w in Resources.FindObjectsOfTypeAll<EditorWindow>()) w.Repaint(); }

    Dictionary<PropertyInfo, bool> propFontSizevalidity;

    bool ValidFontProp(PropertyInfo x, object item)
    {
        if (propFontSizevalidity == null) propFontSizevalidity = new Dictionary<PropertyInfo, bool>();
        if (propFontSizevalidity.ContainsKey(x)) return propFontSizevalidity[x];

        propFontSizevalidity.Add(x, true);

        if (string.IsNullOrEmpty(x.Name)) propFontSizevalidity[x] = false;
        else if (x.PropertyType != typeof(GUIStyle)) propFontSizevalidity[x] = false;
        else if (x.GetValue(item, null) == null) propFontSizevalidity[x] = false;
        else if (((GUIStyle)x.GetValue(item, null)).fontSize < 1) propFontSizevalidity[x] = false;

        return propFontSizevalidity[x];
    }

    void ModifyProp(PropertyInfo x, object item)
    {
        if (!ValidFontProp(x, item)) return;

        var style = ((GUIStyle)x.GetValue(item, null));
        var val = style.fontSize;
        int ret = FontSizeRow(x.Name, val.ToString());
        if (ret == 0 || val + ret <= 0) return;
        style.fontSize = val + ret;
        RepaintAll();
    }

    GUIStyle evenBG;
    GUIStyle oddBG;

    void InitStyles()
    {
        if (evenBG != null) return;
        GUIStyle s = "CN EntryBackEven";
        evenBG = new GUIStyle(s);
        s = "CN EntryBackodd";
        oddBG = new GUIStyle(s);
        evenBG.contentOffset = oddBG.contentOffset = new Vector2();
        evenBG.clipping = oddBG.clipping = TextClipping.Clip;
        evenBG.margin = oddBG.margin =
        evenBG.padding = oddBG.padding = new RectOffset();
    }

    int rowCount = 0;

    void FixZeroSize(GUIStyle s) => s.fontSize = s.fontSize < 1 ? 11 : s.fontSize;

    bool ValidFontStyle(GUIStyle s) => !(s == null || string.IsNullOrEmpty(s.name));

    void FontSizeRow(GUIStyle s)
    {
        if (!ValidFontStyle(s)) return;

        FixZeroSize(s);

        var x = FontSizeRow(s.name, s.fontSize.ToString());

        if (x != 0 && x + s.fontSize > 0) s.fontSize += x;
    }

    int FontSizeRow(string name, string size)
    {
        if (string.IsNullOrEmpty(name) || size == "0") return 0;

        rowCount++;

        var width = GUILayout.MaxWidth(Screen.width);

        using (new GUILayout.HorizontalScope(rowCount % 2 == 0 ? evenBG : oddBG, width))
        {
            GUILayout.Label(name);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("-", EditorStyles.miniButtonLeft)) return -1;

            using (new EditorGUI.DisabledGroupScope(true))
                GUILayout.Label(size, EditorStyles.miniButtonMid, GUILayout.Width(30));

            if (GUILayout.Button("+", EditorStyles.miniButtonRight)) return +1;

        }

        return 0;
    }
}

#endif