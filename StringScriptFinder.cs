#if UNITY_EDITOR

using I2.Loc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

public class StringScriptFinderData : ScriptableObject
{
    public List<UnityEngine.Object> scriptsFound;
    public List<string> skipIfLineContains;
    public List<string> ignoreStrings;
    public List<StringScriptData> scriptDatas;

    public void Initialize()
    {
        scriptsFound = new List<UnityEngine.Object>();
        scriptDatas = new List<StringScriptData>();

        skipIfLineContains = new List<string>();
        skipIfLineContains.Add("Header(");
        skipIfLineContains.Add("[SerializeField]");
        skipIfLineContains.Add("const");
        skipIfLineContains.Add("EditorButton");
        skipIfLineContains.Add("Invoke(");
        skipIfLineContains.Add("AnalyticsConstants.");
        skipIfLineContains.Add("Transition.TransitionOut(");
        skipIfLineContains.Add("LocalizedString");
        skipIfLineContains.Add("InvokeRepeating(");
        skipIfLineContains.Add("Tooltip(");
        skipIfLineContains.Add("DllImport(");
        skipIfLineContains.Add("FindWithTag(");
        skipIfLineContains.Add("Exception(");
        skipIfLineContains.Add("Shader.PropertyToID(");
        skipIfLineContains.Add("ContextMenu(");
        skipIfLineContains.Add("Application.dataPath");
        skipIfLineContains.Add("CompareTag(");

        ignoreStrings = new List<string>();
        ignoreStrings.Add("INV.");
        ignoreStrings.Add("ACH_");
        ignoreStrings.Add("CUR.");
    }
}

[System.Serializable]
public class StringScriptDetails
{
    public string stringText;
    public int lineInScript;
    public string fullLineText;

    public bool foldoutLocalize;
    public string localizeTermKey;
    public string localizeTermCategory;
    public List<string> localizeTermValue;
    public TextMeshProUGUI foundTextComponent;
    public bool isExpanded = false;

    public StringScriptDetails(string stringText, int lineInScript, string fullLineText)
    {
        this.stringText = stringText;
        this.lineInScript = lineInScript;
        this.fullLineText = fullLineText;

        foldoutLocalize = false;

        localizeTermKey = "";
        localizeTermCategory = "";
        localizeTermValue = new List<string>();
    }
}

[System.Serializable]
public class StringScriptData
{
    public UnityEngine.Object scriptObj;
    public List<StringScriptDetails> stringDataDetails;
    public List<string> allLinesInScript;
    public bool mainFoldout;

    public StringScriptData(UnityEngine.Object scriptObj, string[] allLinesInScript)
    {
        this.scriptObj = scriptObj;
        this.allLinesInScript = allLinesInScript.ToList();

        stringDataDetails = new List<StringScriptDetails>();

        mainFoldout = false;
    }

    public void ClearFindings()
    {
        stringDataDetails.Clear();
        allLinesInScript.Clear();
    }
}

[System.Serializable]
public class StringFinderLanguageStruct
{
    public string name;
    public string code;

    public StringFinderLanguageStruct(string name, string code)
    {
        this.name = name;
        this.code = code;
    }
}

public class StringScriptFinder : EditorWindow
{
    [MenuItem("Weyrdlets/Automation/String Script Finder")]
    public static void ShowWindow()
    {
        GetWindow<StringScriptFinder>();
    }

    StringScriptFinderData data;
    SerializedObject serializedObject;
    LanguageSourceAsset languageSource;

    bool excludeCurlyBrackets = true;
    bool excludeHtmlFormatting = true;
    bool excludeAlphabetlessStrings = true;
    bool excludeDebugLogs = true;
    bool excludeCommentedLines = true;

    bool excludeEditorsAndScriptables = true;
    bool excludeEditorOnlys = true;

    string rootFolderPath;

    List<StringFinderLanguageStruct> languages;

    Vector2 resultScrollPos;
    Vector2 scrollPos;

    private void CreateDataInstance()
    {
        data = ScriptableObject.CreateInstance<StringScriptFinderData>();
        data.Initialize();
       
    }

    private bool PreTranslationChecks()
    {
        LocalizationManager.InitializeIfNeeded();
        if (!GoogleTranslation.CanTranslate())
        {
            Debug.LogError($"<color=red>LocalizationHelper: WebService is not set correctly or needs to be reinstalled</color>");
            Debug.LogError($"<color=yellow>LocalizationHelper: Try running a translation in the Language Source Inspector, then try here again.</color>");
            return false;
        }

        return true;
    }

    void SearchForScripts(string startPath)
    {
        data.scriptsFound.Clear();
        data.scriptDatas.Clear();

        string[] startFiles = Directory.GetFiles(startPath, "*.*", SearchOption.AllDirectories);

        List<string> files = new List<string>();
        for (int i = 0; i < startFiles.Length; i++)
        {
            files.Add(startFiles[i]);
        }

        FileAttributes attr = File.GetAttributes(startPath);

        foreach (string file in files)
        {
            attr = File.GetAttributes(file);

            if (attr.HasFlag(FileAttributes.Directory))
            {
                startFiles = Directory.GetFiles(startPath, "*.*", SearchOption.AllDirectories);

                for (int i = 0; i < startFiles.Length; i++)
                {
                    files.Add(startFiles[i]);
                }

                continue;
            }

            if (string.Equals(Path.GetExtension(file), ".cs"))
            {
                data.scriptsFound.Add(AssetDatabase.LoadAssetAtPath(file.Remove(0, Application.dataPath.Length - 6), typeof(UnityEngine.Object)));
            }
        }
    }

    bool GetTextFromScript(UnityEngine.Object stringObj, string stringAssetPath, out string rawTextOutput, out string[] linesInScript)
    {
        StreamReader reader = new StreamReader(stringAssetPath);
        rawTextOutput = reader.ReadToEnd();
        reader.Close();

        linesInScript = File.ReadAllLines(stringAssetPath);

        return (!string.IsNullOrWhiteSpace(rawTextOutput));
    }

    List<string> linesInScriptButTrimmed = new List<string>();

    void SkimTextForStrings(UnityEngine.Object stringObj, string rawTextOutput, string[] linesInScript)
    {
        string searchString = $"\"";
        string searchText = rawTextOutput;
        string lastSearchedText = rawTextOutput;

        if (!rawTextOutput.Contains(searchString)) return;

        bool hasInitIntoResults = false;
        int dataIndex = 0;

        for (int i = 0; i < data.scriptDatas.Count; i++)
        {
            if (data.scriptDatas[i].scriptObj == stringObj)
            {
                hasInitIntoResults = true;
                dataIndex = i;

                data.scriptDatas[i].ClearFindings();
                data.scriptDatas[i].allLinesInScript = linesInScript.ToList();

                break;
            }
        }

        linesInScriptButTrimmed.Clear();
        linesInScriptButTrimmed = linesInScript.ToList();

        if (excludeEditorsAndScriptables && (rawTextOutput.Contains($"using UnityEditor;") || rawTextOutput.Contains($" : ScriptableObject") ))
        {
            return;
        }

        while (true)
        {
            if (!searchText.Contains(searchString)) break;

            lastSearchedText = searchText;

            int index = searchText.IndexOf(searchString);
            
            int startSearchIndex = index + searchString.Length;
            int endSearchIndex = searchText.IndexOf(searchString, startSearchIndex);

            string retrievedText = searchText.Substring(startSearchIndex, endSearchIndex - startSearchIndex);
            string retrievedTextWithQuote = searchText.Substring(index, endSearchIndex - index + searchString.Length);

            string fullStringInScript = "";
            int retrievedTextIndex = 0;

            //! This lets the thing loop, dont break this
            searchText = searchText.Substring(endSearchIndex + 1);

            if (string.IsNullOrWhiteSpace(retrievedText)) continue;

            for (int i = 0; i < linesInScriptButTrimmed.Count; i++)
            {
                if (linesInScriptButTrimmed[i].Contains(retrievedTextWithQuote))
                {
                    retrievedTextIndex = i;
                    linesInScriptButTrimmed[i] = linesInScriptButTrimmed[i].Replace(retrievedText, "");
                    break;
                }
            }

            fullStringInScript = linesInScript[retrievedTextIndex];

            //! Everything below till the very bottom is for filtering
            if (excludeDebugLogs)
            {
                if (fullStringInScript.Contains($"Debug.Log") || fullStringInScript.Contains("Dev.Log") ||
                    fullStringInScript.Contains("Dev.WarningLog") || fullStringInScript.Contains("Dev.ErrorLog")) continue;
            }

            if (excludeCommentedLines)
            {
                if (fullStringInScript.TrimStart()[0] == '/' && fullStringInScript.TrimStart()[1] == '/') continue;

                int searchTextIndexInLast = rawTextOutput.IndexOf(retrievedText, 0, StringComparison.Ordinal);
                int commentedGroupEndIndex = rawTextOutput.IndexOf("*/", searchTextIndexInLast + retrievedText.Length, StringComparison.Ordinal);

                if (commentedGroupEndIndex >= 0)
                {
                    int commentedGroupStartIndex = rawTextOutput.Substring(0, searchTextIndexInLast).IndexOf("/*", 0, StringComparison.Ordinal);
                    int prevCommentedGroupEndIndex = rawTextOutput.Substring(0, searchTextIndexInLast).IndexOf("*/", 0, StringComparison.Ordinal);

                    if (commentedGroupStartIndex >= 0)
                    {
                        if (prevCommentedGroupEndIndex < commentedGroupEndIndex && commentedGroupStartIndex <= prevCommentedGroupEndIndex) continue;

                        if (commentedGroupStartIndex >= 0 && commentedGroupStartIndex < commentedGroupEndIndex &&
                            searchTextIndexInLast > commentedGroupStartIndex && searchTextIndexInLast < commentedGroupEndIndex) continue;
                    }
                    
                }
            }

            if (excludeEditorOnlys)
            {
                if (fullStringInScript.Contains($"UnityEditor.")) continue;

                int searchTextIndexInLast = rawTextOutput.IndexOf(retrievedText, 0, StringComparison.Ordinal);
                int skipGroupEndIndex = rawTextOutput.IndexOf("#endif", searchTextIndexInLast + retrievedText.Length, StringComparison.Ordinal);

                if (skipGroupEndIndex >= 0)
                {
                    int skipGroupStartIndex = rawTextOutput.Substring(0, searchTextIndexInLast).IndexOf("#if UNITY_EDITOR", 0, StringComparison.Ordinal);
                    int prevSkipGroupEndIndex = rawTextOutput.Substring(0, searchTextIndexInLast).IndexOf("#endif", 0, StringComparison.Ordinal);

                    if (skipGroupStartIndex >= 0)
                    {
                        if (prevSkipGroupEndIndex < skipGroupEndIndex && skipGroupStartIndex <= prevSkipGroupEndIndex) continue;

                        if (skipGroupStartIndex >= 0 && skipGroupStartIndex < skipGroupEndIndex &&
                            searchTextIndexInLast > skipGroupStartIndex && searchTextIndexInLast < skipGroupEndIndex) continue;
                    }
                    
                }
            }

            bool skipThisResult = false;

            for (int i = 0; i < data.skipIfLineContains.Count; i++)
            {
                if (fullStringInScript.Contains(data.skipIfLineContains[i]))
                {
                    skipThisResult = true;
                    break;
                }
            }

            if (skipThisResult) continue;

            for (int i = 0; i < data.ignoreStrings.Count; i++)
            {
                if (retrievedText.Contains(data.ignoreStrings[i]))
                {
                    int ignoreIndex = retrievedText.IndexOf(data.ignoreStrings[i], 0, StringComparison.Ordinal);
                    int spaceIndex = retrievedText.IndexOf(' ', ignoreIndex);

                    if (spaceIndex > ignoreIndex)
                    {
                        retrievedText = retrievedText.Remove(ignoreIndex, spaceIndex - ignoreIndex);
                    }
                    else retrievedText = retrievedText.Remove(ignoreIndex).Trim();
                }
            }

            if (excludeCurlyBrackets && retrievedText.Contains('{'))
            {
                while (true)
                {
                    if (!retrievedText.Contains('{')) break;

                    int startRemoveIndex = retrievedText.IndexOf('{');
                    int endRemoveIndex = retrievedText.IndexOf('}');

                    retrievedText = retrievedText.Remove(startRemoveIndex, endRemoveIndex - startRemoveIndex + 1);
                }
            }
            if (excludeHtmlFormatting && retrievedText.Contains('<'))
            {
                while (true)
                {
                    if (!retrievedText.Contains('<')) break;

                    int startRemoveIndex = retrievedText.IndexOf('<');
                    int endRemoveIndex = retrievedText.IndexOf('>');

                    retrievedText = retrievedText.Remove(startRemoveIndex, endRemoveIndex - startRemoveIndex + 1);
                }
            }

            if (excludeAlphabetlessStrings && !retrievedText.Any(x => char.IsLetter(x))) continue;
            if (string.IsNullOrWhiteSpace(retrievedText)) continue;

            if (!hasInitIntoResults)
            {
                hasInitIntoResults = true;
                data.scriptDatas.Add(new StringScriptData(stringObj, linesInScript));
                dataIndex = data.scriptDatas.Count - 1;
            }

            data.scriptDatas[dataIndex].stringDataDetails.Add(new StringScriptDetails(retrievedText.Trim(), retrievedTextIndex + 1, fullStringInScript.Trim()));
        }
    }

    private void OnGUI()
    {
        GUI.enabled = false; EditorGUILayout.ObjectField("Script", MonoScript.FromScriptableObject(this), typeof(StringScriptFinder), false); GUI.enabled = true;
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        if (data == null)
        {
            CreateDataInstance();
            return;
        }
        if (serializedObject == null) serializedObject = new SerializedObject(data);

        serializedObject.Update();

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        languageSource = (LanguageSourceAsset)EditorGUILayout.ObjectField("I2 Language Asset", languageSource, typeof(LanguageSourceAsset), false);

        if (languageSource != null)
        {
            if (languages == null) languages = new List<StringFinderLanguageStruct>();
            if (languages.Count == 0)
            {
                int langLength = languageSource.SourceData.GetLanguages().Count;

                for (int i = 0; i < langLength; i++)
                {
                    languages.Add(new StringFinderLanguageStruct(languageSource.SourceData.GetLanguages()[i],
                                                                    languageSource.SourceData.GetLanguagesCode()[i]));
                }
            }
        }

        EditorGUILayout.BeginHorizontal();
        rootFolderPath = EditorGUILayout.TextField("Root Search Directory", rootFolderPath);
        if (GUILayout.Button("GET Selection", EditorStyles.miniButtonRight, GUILayout.Width(103)))
        {
            rootFolderPath = AssetDatabase.GetAssetPath((DefaultAsset)Selection.activeObject);
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Retrieve All Scripts starting from Directory"))
        {
            string searchPath = $"{Application.dataPath.Remove(Application.dataPath.Length - 6, 6)}{rootFolderPath}";
            SearchForScripts(searchPath);

            Debug.Log($"<color=green>Scripts Found!</color> Total: {data.scriptsFound.Count}");
        }

        EditorGUILayout.Space();

        EditorGUILayout.LabelField($"Filters:", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("skipIfLineContains"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("ignoreStrings"));

        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = excludeCurlyBrackets ? Color.white : Color.green;
        if (GUILayout.Button(excludeCurlyBrackets ? "EXCLUDE Curly Brackets" : "INCLUDE Curly Brackets", GUILayout.Width((EditorGUIUtility.currentViewWidth - 25.0f) * 0.5f))) excludeCurlyBrackets = !excludeCurlyBrackets;

        GUI.backgroundColor = excludeHtmlFormatting ? Color.white : Color.green;
        if (GUILayout.Button(excludeHtmlFormatting ? "EXCLUDE HTML Formats" : "INCLUDE HTML Formats", GUILayout.Width((EditorGUIUtility.currentViewWidth - 25.0f) * 0.5f))) excludeHtmlFormatting = !excludeHtmlFormatting;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = excludeAlphabetlessStrings ? Color.white : Color.green;
        if (GUILayout.Button(excludeAlphabetlessStrings ? "EXCLUDE Alphabetless" : "INCLUDE Alphabetless", GUILayout.Width((EditorGUIUtility.currentViewWidth - 25.0f) * 0.5f))) excludeAlphabetlessStrings = !excludeAlphabetlessStrings;

        GUI.backgroundColor = excludeDebugLogs ? Color.white : Color.green;
        if (GUILayout.Button(excludeDebugLogs ? "EXCLUDE Logs" : "INCLUDE Logs", GUILayout.Width((EditorGUIUtility.currentViewWidth - 25.0f) * 0.5f))) excludeDebugLogs = !excludeDebugLogs;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = excludeCommentedLines ? Color.white : Color.green;
        if (GUILayout.Button(excludeCommentedLines ? "EXCLUDE Commented Lines" : "INCLUDE Commented Lines", GUILayout.Width((EditorGUIUtility.currentViewWidth - 25.0f) * 0.5f))) excludeCommentedLines = !excludeCommentedLines;

        GUI.backgroundColor = excludeEditorsAndScriptables ? Color.white : Color.green;
        if (GUILayout.Button(excludeEditorsAndScriptables ? "EXCLUDE Editor and Scriptables" : "INCLUDE and Scriptables", GUILayout.Width((EditorGUIUtility.currentViewWidth - 25.0f) * 0.5f))) excludeEditorsAndScriptables = !excludeEditorsAndScriptables;
        EditorGUILayout.EndHorizontal();


        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = excludeEditorOnlys ? Color.white : Color.green;
        if (GUILayout.Button(excludeEditorOnlys ? "EXCLUDE Editor Onlys" : "INCLUDE Editor Onlys", GUILayout.Width((EditorGUIUtility.currentViewWidth - 25.0f) * 0.5f))) excludeEditorOnlys = !excludeEditorOnlys;


        EditorGUILayout.EndHorizontal();


        

        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space();

        if (GUILayout.Button("Scan for Strings"))
        {
            data.scriptDatas.Clear();

            for (int i = 0; i < data.scriptsFound.Count; i++)
            {
                string searchPath = $"{Application.dataPath.Remove(Application.dataPath.Length - 6, 6)}{AssetDatabase.GetAssetPath(data.scriptsFound[i])}";

                if (!GetTextFromScript(data.scriptsFound[i], searchPath, out string rawText, out string[] linesInScript)) continue;
                SkimTextForStrings(data.scriptsFound[i], rawText, linesInScript);
            }

            Debug.Log($"<color=green>Scan's Done!</color>");
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"Results: {data.scriptDatas.Count}", EditorStyles.boldLabel);

        for (int i = 0; i < data.scriptDatas.Count; i++)
        {
            data.scriptDatas[i].mainFoldout = EditorGUILayout.Foldout(data.scriptDatas[i].mainFoldout, $"{i}: {data.scriptDatas[i].scriptObj.name}.cs", true);

            if (!data.scriptDatas[i].mainFoldout) continue;

            EditorGUILayout.BeginHorizontal(); EditorGUILayout.LabelField("", GUILayout.Width(25.0f));
            EditorGUILayout.LabelField("Script Reference", GUILayout.Width(EditorGUIUtility.labelWidth - 28.0f));
            GUI.enabled = false;
            EditorGUILayout.ObjectField(data.scriptDatas[i].scriptObj, typeof(UnityEngine.Object), false);
            GUI.enabled = true; EditorGUILayout.EndHorizontal();

            for (int j = 0; j < data.scriptDatas[i].stringDataDetails.Count; j++)
            {
                EditorGUILayout.BeginHorizontal(); EditorGUILayout.LabelField("", GUILayout.Width(25.0f));

                GUI.enabled = false;
                EditorGUILayout.IntField(data.scriptDatas[i].stringDataDetails[j].lineInScript, GUILayout.Width(40.0f));
                GUI.enabled = true;

                data.scriptDatas[i].stringDataDetails[j].stringText = EditorGUILayout.TextField(data.scriptDatas[i].stringDataDetails[j].stringText); EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal(); EditorGUILayout.LabelField("", GUILayout.Width(25.0f));

                GUI.enabled = false;
                EditorGUILayout.TextField(data.scriptDatas[i].stringDataDetails[j].fullLineText);
                GUI.enabled = true;

                if (GUILayout.Button("[ L ]", EditorStyles.miniButtonRight, GUILayout.Width(25.0f)))
                {
                    if (data.scriptDatas[i].stringDataDetails[j].localizeTermValue == null) data.scriptDatas[i].stringDataDetails[j].localizeTermValue = new List<string>();
                    for (int b = data.scriptDatas[i].stringDataDetails[j].localizeTermValue.Count; b < languages.Count; b++) data.scriptDatas[i].stringDataDetails[j].localizeTermValue.Add("");

                    data.scriptDatas[i].stringDataDetails[j].foldoutLocalize = !data.scriptDatas[i].stringDataDetails[j].foldoutLocalize;

                    bool entryExists = false;

                    for (int a = 0; a < languageSource.SourceData.mTerms.Count; a++)
                    {
                        if (string.Equals(data.scriptDatas[i].stringDataDetails[j].stringText, languageSource.SourceData.mTerms[a].GetTranslation(0), StringComparison.Ordinal))
                        {
                            for (int b = 0; b < languages.Count; b++)
                                data.scriptDatas[i].stringDataDetails[j].localizeTermValue[b] = languageSource.SourceData.mTerms[a].GetTranslation(b);

                            string[] splitString = languageSource.SourceData.mTerms[a].Term.Split('/');

                            if (splitString.Length > 1)
                            {
                                data.scriptDatas[i].stringDataDetails[j].localizeTermCategory = splitString[0];
                                data.scriptDatas[i].stringDataDetails[j].localizeTermKey = splitString[1];
                            }
                            else data.scriptDatas[i].stringDataDetails[j].localizeTermKey = splitString[0];

                            entryExists = true;
                            break;
                        }
                    }

                    if (!entryExists) data.scriptDatas[i].stringDataDetails[j].localizeTermValue[0] = data.scriptDatas[i].stringDataDetails[j].stringText;
                }
                EditorGUILayout.EndHorizontal();

                if (data.scriptDatas[i].stringDataDetails[j].foldoutLocalize)
                {
                    EditorGUILayout.Space();

                    EditorGUILayout.BeginHorizontal(); EditorGUILayout.LabelField("", GUILayout.Width(25.0f));
                    EditorGUILayout.LabelField("Localize Term", EditorStyles.boldLabel, GUILayout.Width(EditorGUIUtility.labelWidth - 28.0f));
                    data.scriptDatas[i].stringDataDetails[j].localizeTermKey = EditorGUILayout.TextField(data.scriptDatas[i].stringDataDetails[j].localizeTermKey);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal(); EditorGUILayout.LabelField("", GUILayout.Width(25.0f));
                    EditorGUILayout.LabelField("Category (Optional)", EditorStyles.boldLabel, GUILayout.Width(EditorGUIUtility.labelWidth - 28.0f));
                    data.scriptDatas[i].stringDataDetails[j].localizeTermCategory = EditorGUILayout.TextField(data.scriptDatas[i].stringDataDetails[j].localizeTermCategory);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Space();

                    for (int a = 0; a < languages.Count; a++)
                    {
                        EditorGUILayout.BeginHorizontal(); EditorGUILayout.LabelField("", GUILayout.Width(25.0f));
                        EditorGUILayout.LabelField(languages[a].name, GUILayout.Width(EditorGUIUtility.labelWidth - 28.0f));
                        data.scriptDatas[i].stringDataDetails[j].localizeTermValue[a] = EditorGUILayout.TextField(data.scriptDatas[i].stringDataDetails[j].localizeTermValue[a]);

                        if (a > 0)
                        {
                            if (GUILayout.Button("[ T ]", EditorStyles.miniButtonRight, GUILayout.Width(25.0f)))
                            {
                                if (!PreTranslationChecks()) return;
                                if (string.IsNullOrWhiteSpace(data.scriptDatas[i].stringDataDetails[j].localizeTermValue[0])) return;

                                int dataIndex = i; int detailsIndex = j; int languageIndex = a;

                                GoogleTranslation.Translate(data.scriptDatas[dataIndex].stringDataDetails[detailsIndex].localizeTermValue[0], languages[0].code, languages[a].code,
                                    (translation, error) =>
                                    {
                                        if (!string.IsNullOrWhiteSpace(error))
                                            Debug.LogError($"<color=red>Error Translating: {error}</color>");
                                        else
                                            data.scriptDatas[dataIndex].stringDataDetails[detailsIndex].localizeTermValue[languageIndex] = translation;

                                        AssetDatabase.Refresh();
                                    }
                                );

                                Debug.Log($"<color=green>Translating into</color> {languages[a].name}");
                            }
                        }
                        
                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUILayout.BeginHorizontal(); EditorGUILayout.LabelField("", GUILayout.Width(25.0f));
                    if (GUILayout.Button("Translate All"))
                    {
                        if (!PreTranslationChecks()) return;
                        if (string.IsNullOrWhiteSpace(data.scriptDatas[i].stringDataDetails[j].localizeTermValue[0])) return;

                        for (int a = 1; a < languages.Count; a++)
                        {
                            int dataIndex = i; int detailsIndex = j; int languageIndex = a;

                            GoogleTranslation.Translate(data.scriptDatas[i].stringDataDetails[j].localizeTermValue[0], languages[0].code, languages[a].code,
                                (translation, error) =>
                                {
                                    if (!string.IsNullOrWhiteSpace(error))
                                        Debug.LogError($"<color=red>Error Translating: {error}</color>");
                                    else
                                        data.scriptDatas[dataIndex].stringDataDetails[detailsIndex].localizeTermValue[languageIndex] = translation;

                                    AssetDatabase.Refresh();
                                }
                            );
                        }

                        Debug.Log($"<color=green>Translating All</color>");
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal(); EditorGUILayout.LabelField("", GUILayout.Width(25.0f));
                    if (GUILayout.Button("Insert into Language Source"))
                    {
                        for (int a = 0; a < languages.Count; a++)
                        {
                            if (string.IsNullOrWhiteSpace(data.scriptDatas[i].stringDataDetails[j].localizeTermValue[a]))
                                return;
                        }

                        TermData termData = !string.IsNullOrWhiteSpace(data.scriptDatas[i].stringDataDetails[j].localizeTermCategory) ?
                            languageSource.SourceData.AddTerm($"{data.scriptDatas[i].stringDataDetails[j].localizeTermCategory}/{data.scriptDatas[i].stringDataDetails[j].localizeTermKey}", eTermType.Text) :
                            languageSource.SourceData.AddTerm(data.scriptDatas[i].stringDataDetails[j].localizeTermKey, eTermType.Text);

                        for (int a = 0; a < languages.Count; a++) 
                            termData.SetTranslation(a, data.scriptDatas[i].stringDataDetails[j].localizeTermValue[a]);

                        EditorUtility.SetDirty(languageSource);
                        AssetDatabase.Refresh();
                        AssetDatabase.SaveAssets();

                        Debug.Log($"<color=green>Inserted into Language Source!</color> Please refresh before verifying.");
                    }
                    EditorGUILayout.EndHorizontal();

                    /*
                    EditorGUILayout.BeginHorizontal(); EditorGUILayout.LabelField("", GUILayout.Width(25.0f));
                    if (GUILayout.Button("Replace Line Reference in Script"))
                    {
                        int dataIndex = i; int detailsIndex = j;
                        int lineIndex = data.scriptDatas[dataIndex].stringDataDetails[detailsIndex].lineInScript - 1;

                        if (string.IsNullOrWhiteSpace(data.scriptDatas[dataIndex].stringDataDetails[detailsIndex].localizeTermKey)) return;

                        string replaceStringTarget = data.scriptDatas[dataIndex].stringDataDetails[detailsIndex].stringText;
                        string localizedStringVar = data.scriptDatas[dataIndex].stringDataDetails[detailsIndex].localizeTermKey;

                        if (char.IsUpper(localizedStringVar[0]))
                            localizedStringVar = localizedStringVar.Length == 1 ? char.ToLower(localizedStringVar[0]).ToString() : 
                                                    char.ToLower(localizedStringVar[0]) + localizedStringVar.Substring(1);

                        int quoteIndex = 0;
                        int checkIndex = -1;

                        while (true)
                        {
                            checkIndex = data.scriptDatas[dataIndex].allLinesInScript[lineIndex].IndexOf("\"", checkIndex + 1);
                            if (checkIndex < 0) break;

                            string checkScriptLine = data.scriptDatas[dataIndex].allLinesInScript[lineIndex].Substring(checkIndex + 1);

                            if (!checkScriptLine.Contains(replaceStringTarget)) break;

                            quoteIndex = checkIndex;
                        }

                        data.scriptDatas[dataIndex].allLinesInScript[lineIndex] =
                            data.scriptDatas[dataIndex].allLinesInScript[lineIndex].Replace(replaceStringTarget, "{" + localizedStringVar + "}");

                        if (data.scriptDatas[dataIndex].allLinesInScript[lineIndex][quoteIndex - 1] != '$')
                        {
                            data.scriptDatas[dataIndex].allLinesInScript[lineIndex] =
                                data.scriptDatas[dataIndex].allLinesInScript[lineIndex].Insert(quoteIndex, "$");
                        }

                        for (int a = 0; a < data.scriptDatas[dataIndex].allLinesInScript.Count; a++)
                        {
                            bool containsVoid = data.scriptDatas[dataIndex].allLinesInScript[a].Contains("void");
                            bool containsLocalized = data.scriptDatas[dataIndex].allLinesInScript[a].Contains("I2.Loc.LocalizedString");

                            if (containsVoid || containsLocalized)
                            {
                                int whiteSpaceCounter = 0;
                                for (int b = 0; b < data.scriptDatas[dataIndex].allLinesInScript[a].Length; b++)
                                {
                                    if (char.IsWhiteSpace(data.scriptDatas[dataIndex].allLinesInScript[a][b])) whiteSpaceCounter += 1;
                                    else break;
                                }

                                string insertString = "";
                                for (int b = 0; b < whiteSpaceCounter; b++) insertString += " ";
                                insertString += $"I2.Loc.LocalizedString {localizedStringVar} = \"{data.scriptDatas[dataIndex].stringDataDetails[detailsIndex].localizeTermKey}\";";

                                if (containsVoid) data.scriptDatas[dataIndex].allLinesInScript.Insert(a, $"");
                                data.scriptDatas[dataIndex].allLinesInScript.Insert(containsLocalized ? a + 1 : a, insertString);
                                break;
                            }
                        }

                        string newScriptText = "";
                        for (int a = 0; a < data.scriptDatas[dataIndex].allLinesInScript.Count; a++)
                        {
                            newScriptText += data.scriptDatas[dataIndex].allLinesInScript[a];
                            if (a + 1 < data.scriptDatas[dataIndex].allLinesInScript.Count) newScriptText += System.Environment.NewLine;
                        }
                        string scriptPath = $"{Application.dataPath.Remove(Application.dataPath.Length - 6, 6)}{AssetDatabase.GetAssetPath(data.scriptsFound[i])}";

                        File.WriteAllText(scriptPath, newScriptText);

                        if (GetTextFromScript(data.scriptsFound[i], scriptPath, out string rawText, out string[] linesInScript))
                            SkimTextForStrings(data.scriptsFound[i], rawText, linesInScript);

                        Debug.Log($"<color=green>Localized string added to Script!</color>");

                    }
                   
                    EditorGUILayout.EndHorizontal();
                     */

                    EditorGUILayout.BeginHorizontal(); EditorGUILayout.LabelField("", GUILayout.Width(25.0f));
                    EditorGUILayout.LabelField("", GUI.skin.horizontalSlider); EditorGUILayout.EndHorizontal();

                }

                EditorGUILayout.Space();
            }

            

            EditorGUILayout.Space();
        }



        EditorGUILayout.Space();
        EditorGUILayout.Space();
        EditorGUILayout.Space();
        EditorGUILayout.Space();
        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("scriptsFound"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("scriptDatas"));

        EditorGUILayout.EndScrollView();

        if (serializedObject.hasModifiedProperties) serializedObject.ApplyModifiedProperties();
    }
}

#endif