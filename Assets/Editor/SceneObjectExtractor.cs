using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System;

public class SceneObjectExtractor : EditorWindow
{
    bool onlyExportWithRelevantComponents = true;
    bool savePngPreview = true;
    int previewSize = 512;
    Color previewBackground = new Color(0.18f, 0.18f, 0.18f, 1f);

    static string EscapeCsv(string value)
    {
        if (value == null) return "\"\"";
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    [MenuItem("Tools/Extract Scene Object Data")]
    public static void ShowWindow()
    {
        GetWindow<SceneObjectExtractor>("Scene Object Extractor");
    }

    void OnGUI()
    {
        if (GUILayout.Button("Export Hierarchy & Material Data"))
        {
            ExtractData();
        }
        onlyExportWithRelevantComponents = EditorGUILayout.ToggleLeft("Only export GameObjects with Rigidbody / Collider / MonoBehaviour", onlyExportWithRelevantComponents);
        if (GUILayout.Button("Export All GameObjects to FBX"))
        {
            ExportAllToFbx();
        }

        savePngPreview = EditorGUILayout.ToggleLeft("Also save PNG preview (same name as GameObject)", savePngPreview);
        using (new EditorGUI.DisabledScope(!savePngPreview))
        {
            previewSize = EditorGUILayout.IntPopup("Preview Size", previewSize, new[] { "256", "512", "1024" }, new[] { 256, 512, 1024 });
            previewBackground = EditorGUILayout.ColorField("Preview Background", previewBackground);
        }
    }

    void ExtractData()
    {
        string path = EditorUtility.SaveFilePanel("Save Data", "", "SceneData.csv", "csv");
        if (string.IsNullOrEmpty(path)) return;

        StreamWriter writer = new StreamWriter(path);
        writer.WriteLine("ObjectName,Position,MeshName,MaterialName");

        // 查找场景中所有的GameObject
        GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();

        foreach (GameObject obj in allObjects)
        {
            // 排除隐藏对象或UI元素（根据需求）
            if (obj.hideFlags != HideFlags.None) continue;

            string objName = obj.name;
            string pos = obj.transform.position.ToString();
            string meshName = "None";
            string matName = "None";

            // 提取Mesh信息
            MeshFilter mf = obj.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                meshName = mf.sharedMesh.name;
            }

            // 提取Material信息
            Renderer rd = obj.GetComponent<Renderer>();
            if (rd != null && rd.sharedMaterial != null)
            {
                matName = rd.sharedMaterial.name;
            }

            // 写入CSV（对字段进行转义，避免逗号被误识别为分隔符）
            writer.WriteLine($"{EscapeCsv(objName)},{EscapeCsv(pos)},{EscapeCsv(meshName)},{EscapeCsv(matName)}");
        }

        writer.Close();
        Debug.Log("Scene data exported to: " + path);
    }

    void ExportAllToFbx()
    {
        string folder = EditorUtility.SaveFolderPanel("Export FBX Folder", "", "");
        if (string.IsNullOrEmpty(folder)) return;

        // 尝试更可靠地找到 FBX 的 ModelExporter 类型（优先寻找类型名为 ModelExporter 的类型）
        System.Type exporterType = null;
        var candidateTypes = new System.Collections.Generic.List<System.Type>();
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type[] types;
            try { types = asm.GetTypes(); } catch { continue; }
            foreach (var t in types)
            {
                if (t == null || string.IsNullOrEmpty(t.Name)) continue;
                if (string.Equals(t.Name, "ModelExporter", System.StringComparison.OrdinalIgnoreCase))
                {
                    exporterType = t;
                    break;
                }
                var fullname = (t.FullName ?? "").ToLowerInvariant();
                if (fullname.Contains(".fbx") && fullname.Contains("export"))
                {
                    candidateTypes.Add(t);
                }
            }
            if (exporterType != null) break;
        }

        if (exporterType == null && candidateTypes.Count > 0)
        {
            // 取第一个更可能的候选
            exporterType = candidateTypes[0];
        }

        if (exporterType == null)
        {
            Debug.LogError("FBX Exporter package not found. Please install 'com.unity.formats.fbx' from Package Manager.");
            return;
        }

        // 尝试匹配常见导出方法签名（优先 ModelExporter.ExportObject/ExportObjects）
        MethodInfo exportMethod = null;
        // 可能的候选方法名与签名模式
        var tryMethods = new (string name, System.Type[] paramTypes)[] {
            ("ExportObject", new System.Type[]{ typeof(string), typeof(GameObject) }),
            ("ExportObjects", new System.Type[]{ typeof(string), typeof(GameObject[]) }),
            ("Export", new System.Type[]{ typeof(string), typeof(GameObject) }),
            ("ExportModel", new System.Type[]{ typeof(string), typeof(GameObject) }),
            ("ExportToFile", new System.Type[]{ typeof(string), typeof(GameObject) })
        };

        foreach (var cand in tryMethods)
        {
            exportMethod = exporterType.GetMethod(cand.name, BindingFlags.Public | BindingFlags.Static, null, cand.paramTypes, null);
            if (exportMethod != null) break;
        }

        // 退回到查找任何静态方法的策略（宽松匹配）
        if (exportMethod == null)
        {
            foreach (var m in exporterType.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                var n = m.Name.ToLowerInvariant();
                if (n.Contains("export") && m.GetParameters().Length >= 1)
                {
                    exportMethod = m;
                    break;
                }
            }
        }

        if (exportMethod == null)
        {
            Debug.LogError("FBX Exporter API not found on detected type: " + exporterType.FullName);
            return;
        }
        GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();
        int exportedCount = 0;
        int previewCount = 0;
        foreach (GameObject obj in allObjects)
        {
            if (obj.hideFlags != HideFlags.None) continue;
            if (onlyExportWithRelevantComponents && !ShouldExportObject(obj)) continue;
            long fileId = GetObjectFileID(obj);
            string safeName = SanitizeFileName(obj.name);
            string baseName = fileId != 0 ? $"{fileId}-{safeName}" : safeName;
            string file = Path.Combine(folder, baseName + ".fbx");
            try
            {
                var pars = exportMethod.GetParameters();
                if (pars.Length == 2 && pars[0].ParameterType == typeof(string) )
                {
                    // 如果第二个参数接受 UnityEngine.Object 或 GameObject
                    if (pars[1].ParameterType.IsAssignableFrom(typeof(UnityEngine.Object)))
                    {
                        exportMethod.Invoke(null, new object[]{ file, obj });
                    }
                    else if (pars[1].ParameterType.IsArray)
                    {
                        var arr = System.Array.CreateInstance(pars[1].ParameterType.GetElementType(), 1);
                        arr.SetValue(obj, 0);
                        exportMethod.Invoke(null, new object[]{ file, arr });
                    }
                    else
                    {
                        // 尝试直接传入 GameObject
                        exportMethod.Invoke(null, new object[]{ file, obj });
                    }
                }
                else if (pars.Length == 1 && pars[0].ParameterType == typeof(string))
                {
                    // 一些导出器可能只需要路径（少见）
                    exportMethod.Invoke(null, new object[]{ file });
                }
                else
                {
                    // 尝试使用单参数 (string, Object[])
                    exportMethod.Invoke(null, new object[]{ file, new UnityEngine.Object[]{ obj } });
                }
                Debug.Log("Exported FBX: " + file);
                exportedCount++;

                if (savePngPreview)
                {
                    string pngPath = Path.Combine(folder, baseName + ".png");
                    if (TrySaveGameObjectPreviewPng(obj, pngPath, previewSize, previewBackground))
                    {
                        previewCount++;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError("Failed exporting '" + obj.name + "' : " + ex.Message);
            }
        }

        Debug.Log($"FBX export finished. Exported count: {exportedCount}, PNG previews saved: {previewCount}");
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// Get Object FileID based on GlobalObjectId string parsing.
    /// </summary>
    static long GetObjectFileID(UnityEngine.Object obj)
    {
        if (obj == null)
        {
            Debug.LogWarning("GetObjectFileID: Object is null!");
            return 0;
        }

        GlobalObjectId gid = GlobalObjectId.GetGlobalObjectIdSlow(obj);
        string gidString = gid.ToString();
        string[] parts = gidString.Split('-');

        if (parts.Length < 2)
        {
            Debug.LogWarning("GlobalObjectId format unexpected: " + gidString);
            return 0;
        }

        long fileID;
        if (obj is GameObject go)
        {
            // prefab instance: use last segment
            // normal objects: use second last
            if (go.scene.isLoaded && PrefabUtility.IsPartOfPrefabInstance(go))
            {
                if (long.TryParse(parts[parts.Length - 1], out fileID))
                    return fileID;
            }
            else
            {
                if (long.TryParse(parts[parts.Length - 2], out fileID))
                    return fileID;
            }
        }
        else if (obj is Component comp)
        {
            GameObject goComp = comp.gameObject;

            // prefab instance: component file id in second last
            if (goComp.scene.isLoaded && PrefabUtility.IsPartOfPrefabInstance(goComp))
            {
                if (long.TryParse(parts[parts.Length - 2], out fileID))
                    return fileID;
            }
            else
            {
                if (long.TryParse(parts[parts.Length - 2], out fileID))
                    return fileID;
            }
        }

        Debug.LogWarning("Failed to parse FileID from GlobalObjectId: " + gidString);
        return 0;
    }

    static string SanitizeFileName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Unnamed";
        var invalid = Path.GetInvalidFileNameChars();
        foreach (var c in invalid)
        {
            name = name.Replace(c, '_');
        }
        return name.Trim();
    }

    static bool TrySaveGameObjectPreviewPng(GameObject source, string pngPath, int size, Color background)
    {
        if (source == null) return false;
        if (size <= 0) size = 512;

        PreviewRenderUtility preview = null;
        GameObject proxyRoot = null;
        var bakedMeshes = new List<Mesh>();
        Texture2D tex = null;
        try
        {
            preview = new PreviewRenderUtility(true);
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = background;
            preview.camera.fieldOfView = 30f;
            preview.camera.nearClipPlane = 0.01f;

            preview.lights[0].intensity = 1.3f;
            preview.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0f);
            preview.lights[1].intensity = 1.0f;
            preview.lights[1].transform.rotation = Quaternion.Euler(340f, 220f, 0f);
            preview.ambientColor = new Color(0.6f, 0.6f, 0.6f, 1f);

            proxyRoot = BuildRenderProxy(source, bakedMeshes);
            if (proxyRoot == null) return false;

            var bounds = CalculateBounds(proxyRoot);
            if (bounds.size == Vector3.zero) return false;

            PositionCameraToBounds(preview.camera, bounds);

            proxyRoot.hideFlags = HideFlags.HideAndDontSave;
            preview.AddSingleGO(proxyRoot);

            preview.BeginStaticPreview(new Rect(0, 0, size, size));
            preview.Render(true);
            tex = preview.EndStaticPreview();
            if (tex == null) return false;

            var bytes = tex.EncodeToPNG();
            File.WriteAllBytes(pngPath, bytes);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed saving preview for '{source.name}': {ex.Message}");
            return false;
        }
        finally
        {
            if (tex != null)
            {
                DestroyImmediate(tex);
            }
            if (preview != null)
            {
                preview.Cleanup();
            }
            if (proxyRoot != null)
            {
                DestroyImmediate(proxyRoot);
            }
            for (int i = 0; i < bakedMeshes.Count; i++)
            {
                if (bakedMeshes[i] != null)
                {
                    DestroyImmediate(bakedMeshes[i]);
                }
            }
        }
    }

    static GameObject BuildRenderProxy(GameObject source, List<Mesh> bakedMeshes)
    {
        var renderers = source.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return null;

        var root = new GameObject(source.name + "_PreviewProxy");
        root.hideFlags = HideFlags.HideAndDontSave;

        foreach (var r in renderers)
        {
            if (r == null) continue;
            if (r is ParticleSystemRenderer) continue;
            if (!r.enabled) continue;

            var relPath = GetRelativePath(source.transform, r.transform);
            var proxyNode = GetOrCreatePath(root.transform, source.transform, r.transform, relPath);

            if (r is MeshRenderer)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;

                var pmf = proxyNode.gameObject.GetComponent<MeshFilter>();
                if (pmf == null) pmf = proxyNode.gameObject.AddComponent<MeshFilter>();
                pmf.sharedMesh = mf.sharedMesh;

                var pmr = proxyNode.gameObject.GetComponent<MeshRenderer>();
                if (pmr == null) pmr = proxyNode.gameObject.AddComponent<MeshRenderer>();
                pmr.sharedMaterials = r.sharedMaterials;
            }
            else if (r is SkinnedMeshRenderer smr)
            {
                if (smr.sharedMesh == null) continue;
                var baked = new Mesh();
                smr.BakeMesh(baked);
                baked.name = smr.sharedMesh.name + "_Baked";
                bakedMeshes.Add(baked);

                var pmf = proxyNode.gameObject.GetComponent<MeshFilter>();
                if (pmf == null) pmf = proxyNode.gameObject.AddComponent<MeshFilter>();
                pmf.sharedMesh = baked;

                var pmr = proxyNode.gameObject.GetComponent<MeshRenderer>();
                if (pmr == null) pmr = proxyNode.gameObject.AddComponent<MeshRenderer>();
                pmr.sharedMaterials = smr.sharedMaterials;
            }
        }

        // if nothing got added (all filtered out)
        if (root.GetComponentsInChildren<Renderer>(true).Length == 0)
        {
            DestroyImmediate(root);
            return null;
        }

        return root;
    }

    static string GetRelativePath(Transform root, Transform target)
    {
        if (root == target) return string.Empty;
        var stack = new Stack<string>();
        var t = target;
        while (t != null && t != root)
        {
            stack.Push(t.name);
            t = t.parent;
        }
        return string.Join("/", stack.ToArray());
    }

    static Transform GetOrCreatePath(Transform proxyRoot, Transform sourceRoot, Transform sourceTarget, string relPath)
    {
        if (string.IsNullOrEmpty(relPath))
        {
            // map root transform
            proxyRoot.localPosition = sourceRoot.localPosition;
            proxyRoot.localRotation = sourceRoot.localRotation;
            proxyRoot.localScale = sourceRoot.localScale;
            return proxyRoot;
        }

        var parts = relPath.Split('/');
        var current = proxyRoot;
        var srcCurrent = sourceRoot;
        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            var child = current.Find(part);
            if (child == null)
            {
                var go = new GameObject(part);
                go.hideFlags = HideFlags.HideAndDontSave;
                child = go.transform;
                child.SetParent(current, false);
            }

            // advance source transform to keep local TRS aligned
            var srcChild = srcCurrent.Find(part);
            if (srcChild != null)
            {
                child.localPosition = srcChild.localPosition;
                child.localRotation = srcChild.localRotation;
                child.localScale = srcChild.localScale;
                srcCurrent = srcChild;
            }

            current = child;
        }

        // Ensure the leaf matches the actual renderer transform if possible
        if (sourceTarget != null)
        {
            current.localPosition = sourceTarget.localPosition;
            current.localRotation = sourceTarget.localRotation;
            current.localScale = sourceTarget.localScale;
        }
        return current;
    }

    static Bounds CalculateBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds b = new Bounds(Vector3.zero, Vector3.zero);
        bool hasBounds = false;
        foreach (var r in renderers)
        {
            if (r == null) continue;
            if (!hasBounds)
            {
                b = r.bounds;
                hasBounds = true;
            }
            else
            {
                b.Encapsulate(r.bounds);
            }
        }
        return b;
    }

    static void PositionCameraToBounds(Camera cam, Bounds bounds)
    {
        if (cam == null) return;

        var center = bounds.center;
        var radius = bounds.extents.magnitude;
        if (radius < 0.0001f) radius = 0.5f;

        // Fit bounds into view
        float fovRad = cam.fieldOfView * Mathf.Deg2Rad;
        float dist = radius / Mathf.Sin(fovRad * 0.5f);
        dist *= 1.15f;

        var dir = new Vector3(1f, 0.9f, -1.2f).normalized;
        cam.transform.position = center + dir * dist;
        cam.transform.LookAt(center);
        cam.farClipPlane = dist + radius * 4f;
    }

    bool ShouldExportObject(GameObject obj)
    {
        if (obj.GetComponent<Rigidbody>() != null) return true;
        if (obj.GetComponent<Collider>() != null) return true;

        // Note: Missing Script components can appear as null MonoBehaviours.
        // We only treat non-null MonoBehaviours as relevant.
        var behaviours = obj.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null) return true;
        }
        return false;
    }
}
