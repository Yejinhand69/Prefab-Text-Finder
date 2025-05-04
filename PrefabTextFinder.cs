#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using TMPro;
using I2.Loc;
using UnityEditor.SceneManagement;

public class PrefabTextFinder : EditorWindow
{
    private Dictionary<string, List<GameObject>> scenePrefabs = new Dictionary<string, List<GameObject>>();
    private List<GameObject> prefabsWithTextMeshPro = new List<GameObject>();
    private int itemsPerPage = 10;
    private int currentPage = 0;
    private List<StringScriptDetails> prefabTextData = new List<StringScriptDetails>();
    private LanguageSourceAsset languageSource;
    private List<StringFinderLanguageStruct> languages;
    Vector2 scrollPos;

    [MenuItem("Weyrdlets/Automation/Prefab Text Finder")]
    public static void ShowWindow()
    {
        GetWindow<PrefabTextFinder>("PrefabTextFinder");
    }

    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
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

        //region--------------------------------This is for seeing prefabs according to scenes--------------------------------//
        //if (GUILayout.Button("Find Prefabs by Scene Order", GUILayout.ExpandWidth(true)))
        //{
        //    FindPrefabsBySceneOrder();
        //}

        //foreach (var scene in scenePrefabs.Keys)
        //{
        //    EditorGUILayout.LabelField($"Scene: {scene}", EditorStyles.boldLabel);
        //    List<GameObject> prefabs = scenePrefabs[scene];
        //    for (int i = currentPage * itemsPerPage; i < Mathf.Min((currentPage + 1) * itemsPerPage, prefabs.Count); i++)
        //    {
        //        EditorGUILayout.BeginHorizontal();
        //        EditorGUILayout.LabelField((i + 1).ToString() + ".", GUILayout.Width(20));
        //        if (GUILayout.Button(prefabs[i].name, GUILayout.ExpandWidth(true)))
        //        {
        //            Selection.activeObject = prefabs[i];
        //        }
        //        EditorGUILayout.EndHorizontal();
        //    }
        //}
        //endregion--------------------------------This is for seeing prefabs according to scenes--------------------------------//

        GUI.backgroundColor = new Color(1.0f, 1.0f, 0.7f);//Light yellow button (ease looking)
        if (GUILayout.Button("Find Prefabs with TextMeshProUGUI (Scan project)", GUILayout.ExpandWidth(true)))
        {
            FindPrefabsWithTextMeshPro();
        }
        GUI.backgroundColor = Color.white;

        if (prefabsWithTextMeshPro.Count > 0)
        {
            EditorGUILayout.LabelField($"Page {currentPage + 1} / {Mathf.CeilToInt((float)prefabsWithTextMeshPro.Count / itemsPerPage)}", GUILayout.ExpandWidth(true));

            for (int i = currentPage * itemsPerPage; i < Mathf.Min((currentPage + 1) * itemsPerPage, prefabsWithTextMeshPro.Count); i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField((i + 1).ToString() + ".", GUILayout.Width(20));
                if (GUILayout.Button(prefabsWithTextMeshPro[i].name, GUILayout.ExpandWidth(true)))
                {
                    Selection.activeObject = prefabsWithTextMeshPro[i];
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(1.0f, 1.0f, 0.8f);//Light yellow button (ease looking)
            if (GUILayout.Button("Previous Page", GUILayout.ExpandWidth(true)) && currentPage > 0)
            {
                currentPage--;
            }
            if (GUILayout.Button("Next Page", GUILayout.ExpandWidth(true)) && (currentPage + 1) * itemsPerPage < prefabsWithTextMeshPro.Count)
            {
                currentPage++;
            }
            EditorGUILayout.EndHorizontal();
            GUI.backgroundColor = Color.white;
        }

        itemsPerPage = EditorGUILayout.IntSlider("Prefabs Per Page", itemsPerPage, 1, 10);

        EditorGUILayout.Space(10);

        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);//Green button (ease looking)
        if (GUILayout.Button("Find TextMeshProUGUI in Opened Prefab", GUILayout.ExpandWidth(true)))
        {
            FindTextMeshProInOpenPrefab();
        }
        GUI.backgroundColor = Color.white;

        if (prefabTextData.Count > 0)
        {
            for(int a = 0;  a < prefabTextData.Count; a++) 
            {
                var textData = prefabTextData[a];

                EditorGUILayout.Space(10);
                
                if (textData.foundTextComponent != null)
                {
                    textData.isExpanded = EditorGUILayout.Foldout(textData.isExpanded, $"[{a + 1}] {textData.foundTextComponent.gameObject.name}", true, EditorStyles.foldoutHeader);
                }

                if (textData.isExpanded)
                {
                    EditorGUI.indentLevel++;

                    if (textData.foundTextComponent != null)
                    {
                        EditorGUILayout.LabelField($"GameObject: {textData.foundTextComponent.gameObject.name}", EditorStyles.boldLabel);
                    }

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Text: ", GUILayout.Width(80));
                    GUIStyle blueTextStyle = new GUIStyle(EditorStyles.label);
                    blueTextStyle.normal.textColor = new Color(0.4f, 0.7f, 1.0f);//Light blue found text (ease looking)

                    if (GUILayout.Button(textData.stringText, blueTextStyle, GUILayout.ExpandWidth(true)))
                    {
                        if (textData.foundTextComponent != null)
                        {
                            Selection.activeObject = textData.foundTextComponent;
                            EditorGUIUtility.PingObject(textData.foundTextComponent);
                        }
                    }
                    EditorGUILayout.EndHorizontal();

                    GUIStyle boldLabel = new GUIStyle(EditorStyles.boldLabel);

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Term Key", boldLabel, GUILayout.Width(80));
                    textData.localizeTermKey = EditorGUILayout.TextField(textData.localizeTermKey, GUILayout.ExpandWidth(true));
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Category", boldLabel, GUILayout.Width(80));
                    textData.localizeTermCategory = EditorGUILayout.TextField(textData.localizeTermCategory, GUILayout.ExpandWidth(true));
                    EditorGUILayout.EndHorizontal();

                    #region _GoogleTranslationRegion
                    if (languages != null && languages.Count > 0)
                    {
                        for (int i = 0; i < languages.Count; i++)
                        {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField("", GUILayout.Width(0.0f));
                            EditorGUILayout.LabelField(languages[i].name, GUILayout.Width(77));

                            while (textData.localizeTermValue.Count <= i)
                            {
                                textData.localizeTermValue.Add("");
                            }

                            textData.localizeTermValue[i] = EditorGUILayout.TextField(textData.localizeTermValue[i], GUILayout.ExpandWidth(true));

                            if (i > 0)
                            {
                                if (GUILayout.Button("[ T ]", EditorStyles.miniButtonRight, GUILayout.Width(25.0f)))
                                {
                                    if (string.IsNullOrWhiteSpace(textData.localizeTermValue[0])) return;//prevent empty

                                    int langIndex = i;

                                    GoogleTranslation.Translate(textData.localizeTermValue[0], languages[0].code, languages[langIndex].code,
                                        (translation, error) =>
                                        {
                                            if (!string.IsNullOrWhiteSpace(error))
                                                Debug.LogError($"<color=red>Error Translating: {error}</color>");
                                            else
                                                textData.localizeTermValue[langIndex] = translation;

                                            AssetDatabase.Refresh();
                                        }
                                    );

                                    Debug.Log($"<color=green>Translating into</color> {languages[langIndex].name}");
                                }
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                    }

                    GUI.backgroundColor = new Color(0.5f, 0.8f, 0.5f);//Light green button (ease looking)
                    if (GUILayout.Button("Translate All"))
                    {
                        for (int i = 1; i < languages.Count; i++)
                        {
                            if (string.IsNullOrWhiteSpace(textData.localizeTermValue[0])) return;

                            int langIndex = i;

                            GoogleTranslation.Translate(textData.localizeTermValue[0], languages[0].code, languages[langIndex].code,
                                (translation, error) =>
                                {
                                    if (!string.IsNullOrWhiteSpace(error))
                                        Debug.LogError($"<color=red>Error Translating: {error}</color>");
                                    else
                                        textData.localizeTermValue[langIndex] = translation;

                                    AssetDatabase.Refresh();
                                }
                            );

                            Debug.Log($"<color=green>Translating into</color> {languages[langIndex].name}");
                        }
                    }
                    GUI.backgroundColor = Color.white;
                    #endregion

                    GUI.backgroundColor = new Color(1.0f, 1.0f, 0.7f);//Light yellow button (ease looking)
                    if (GUILayout.Button("Insert into Language Source", GUILayout.ExpandWidth(true)))
                    {
                        InsertIntoLanguageSource(textData);
                    }
                    GUI.backgroundColor = Color.white;
                }

            }
        }
        EditorGUILayout.EndScrollView();
}
    #region _SortPrefabsAccordingToScene(DeveloperMode)
    private void FindPrefabsBySceneOrder()
    {
        scenePrefabs.Clear();
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        SceneAsset[] scenes = EditorBuildSettings.scenes.Select(s => AssetDatabase.LoadAssetAtPath<SceneAsset>(s.path)).ToArray();

        foreach (var scene in scenes)
        {
            scenePrefabs[scene.name] = new List<GameObject>();
        }

        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && prefab.GetComponentsInChildren<TextMeshProUGUI>(true).Length > 0)
            {
                string sceneName = scenes.FirstOrDefault(s => path.Contains(s.name))?.name ?? "Uncategorized";
                if (!scenePrefabs.ContainsKey(sceneName))
                {
                    scenePrefabs[sceneName] = new List<GameObject>();
                }
                scenePrefabs[sceneName].Add(prefab);
            }
        }
    }
    #endregion

    private void FindPrefabsWithTextMeshPro()
    {
        prefabsWithTextMeshPro.Clear();
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            //Only retrieve the ones with TmpUGUI type-specific-component
            if (prefab != null && prefab.GetComponentsInChildren<TextMeshProUGUI>(true).Length > 0)
            {
                prefabsWithTextMeshPro.Add(prefab);
            }
        }

        //Sort the list in alphabet order
        prefabsWithTextMeshPro = prefabsWithTextMeshPro.OrderBy(p => p.name).ToList();
    }

    private void FindTextMeshProInOpenPrefab()
    {
        prefabTextData.Clear();

        GameObject prefabRoot = PrefabStageUtility.GetCurrentPrefabStage()?.prefabContentsRoot;
        if (prefabRoot == null)
        {
            Debug.LogWarning("No prefab is currently open for editing.");
            return;
        }

        foreach (var tmp in prefabRoot.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            var textData = new StringScriptDetails(tmp.text, -1, tmp.text)
            {
                localizeTermKey = tmp.name,
                localizeTermCategory = "",
                localizeTermValue = new List<string>(),
                foundTextComponent = tmp
            };

            //localizeTermValue has at least one entry
            while (textData.localizeTermValue.Count <= 0)
            {
                textData.localizeTermValue.Add("");
            }

            //this is auto filling TMP into the default "English" slot, so you don't have to manually type it every time.
            textData.localizeTermValue[0] = tmp.text;

            //Checks if the term already exist in the I2 database
            if(languageSource != null)
            {
                string fullTermPath = FindExistingTerm(tmp.text);

                if (!string.IsNullOrEmpty(fullTermPath))
                {
                    Debug.Log($"<color=yellow>Term '{fullTermPath}' exists. Auto-filling translations.</color>");

                    TermData existingTerm = languageSource.SourceData.GetTermData(fullTermPath);
                    if (existingTerm != null)
                    {
                        textData.localizeTermKey = GetKeyFromFullPath(fullTermPath);
                        textData.localizeTermCategory = GetCategoryFromFullPath(fullTermPath);

                        for (int j = 0; j < languages.Count; j++)
                        {
                            string translation = existingTerm.GetTranslation(j);
                            while (textData.localizeTermValue.Count <= j)
                            {
                                textData.localizeTermValue.Add("");
                            }
                            textData.localizeTermValue[j] = translation;
                        }
                    }
                }
            }
            prefabTextData.Add(textData);
        }
    }

    #region _HelperMethods
    private string FindExistingTerm(string searchText)
    {
        foreach (var term in languageSource.SourceData.mTerms)
        {
            if (term.Languages[0] == searchText) 
            {
                return term.Term;
            }
        }
        return null;
    }
    private string GetKeyFromFullPath(string fullPath)
    {
        if (fullPath.Contains("/"))
            return fullPath.Substring(fullPath.LastIndexOf('/') + 1);
        return fullPath;
    }
    private string GetCategoryFromFullPath(string fullPath)
    {
        if (fullPath.Contains("/"))
            return fullPath.Substring(0, fullPath.LastIndexOf('/'));
        return "";
    }
    #endregion

    private void InsertIntoLanguageSource(StringScriptDetails textData)
    {
        if (languageSource == null)
        {
            Debug.LogError("No Language Source Asset assigned.");
            return;
        }

        string termKey = textData.localizeTermKey;
        TermData existingTerm = languageSource.SourceData.GetTermData(termKey);

        if(existingTerm != null)
        {
            Debug.Log($"<color=yellow>Term '{termKey}' already exists in the Language Source. No need to insert.</color>");
            return;
        }

        foreach (var translation in textData.localizeTermValue)
        {
            if (string.IsNullOrWhiteSpace(translation))
                return;
        }

        TermData termData = !string.IsNullOrWhiteSpace(textData.localizeTermCategory) ?
            languageSource.SourceData.AddTerm($"{textData.localizeTermCategory}/{textData.localizeTermKey}", eTermType.Text) :
            languageSource.SourceData.AddTerm(textData.localizeTermKey, eTermType.Text);

        for (int i = 0; i < textData.localizeTermValue.Count; i++)
            termData.SetTranslation(i, textData.localizeTermValue[i]);

        EditorUtility.SetDirty(languageSource);
        AssetDatabase.Refresh();
        AssetDatabase.SaveAssets();

        Debug.Log("<color=green>Inserted into Language Source! Please refresh before verifying.");
    }
}

#endif
