using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Menu do editor: Asylum > Montar fase 2
public static class Fase2Menu
{
    const string ControllerPath = "Assets/Creep Horror Creature/CreepController.controller";
    const string CreatureModelPath = "Assets/Creep Horror Creature/Meshes/Creep_mesh.fbx";

    static StringBuilder report;

    [MenuItem("Asylum/Montar fase 2")]
    public static void Montar()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[Fase2] Saia do Play antes de montar a fase 2.");
            return;
        }
        report = new StringBuilder();

        GameObject old = GameObject.Find("_Fase2");
        if (old != null) Undo.DestroyObjectImmediate(old);
        GameObject root = new GameObject("_Fase2");
        Undo.RegisterCreatedObjectUndo(root, "Montar fase 2");

        Random.State saved = Random.state;
        Random.InitState(1974);
        HashSet<string> used = new HashSet<string>();

        EnemyAI enemy = Object.FindFirstObjectByType<EnemyAI>();
        Vector3 enemyPos = enemy != null ? enemy.transform.position : Vector3.zero;
        NavMeshHit eh;
        if (NavMesh.SamplePosition(enemyPos, out eh, 4f, NavMesh.AllAreas)) enemyPos = eh.position;

        Material metal = Mat("Fase2_Metal", new Color(0.16f, 0.17f, 0.18f), 0.7f, 0.35f);
        Material painel = Mat("Fase2_Painel", new Color(0.45f, 0.43f, 0.36f), 0.3f, 0.3f);
        Material vermelho = Mat("Fase2_Vermelho", new Color(0.6f, 0.06f, 0.05f), 0.2f, 0.5f);
        Material ceramica = Mat("Fase2_Fusivel", new Color(0.85f, 0.8f, 0.68f), 0f, 0.4f);
        Material latao = Mat("Fase2_Latao", new Color(0.8f, 0.62f, 0.25f), 0.9f, 0.6f);
        Material papel = Mat("Fase2_Papel", new Color(0.86f, 0.83f, 0.72f), 0f, 0.1f);

        // Armadilha
        string trapRoom = CreateTrap(root.transform, enemyPos, used, metal, vermelho);

        // Gerador
        string genRoom = CreateGenerator(root.transform, enemyPos, used, metal, painel);

        // Fusiveis
        string[][] fuseRooms =
        {
            new[] { "Gatehouse", "GuardRoom", "VisitRoom" },
            new[] { "KitchenStock", "Stock", "KitchenA" },
            new[] { "Morgue", "SeclusionRoomB", "HallUnderground" }
        };
        Vector3 notePos = Vector3.zero;
        bool hasNotePos = false;
        for (int i = 0; i < fuseRooms.Length; i++)
        {
            Vector3 pos; string room;
            if (!FindSpot(fuseRooms[i], used, out pos, out room))
            {
                report.AppendLine("Fusivel " + (i + 1) + ": SEM LUGAR");
                continue;
            }
            CreateFuse(i + 1, pos, root.transform, ceramica, latao);
            report.AppendLine("Fusivel " + (i + 1) + ": " + room + " " + pos + " criatura alcanca=" + Reachable(enemyPos, pos));
            if (i == 1) { notePos = pos; hasNotePos = true; }
        }

        // Bilhete perto do segundo fusivel
        if (hasNotePos)
        {
            Vector3 p = notePos + new Vector3(0.7f, 0f, 0.4f);
            NavMeshHit nh;
            if (NavMesh.SamplePosition(p, out nh, 1.5f, NavMesh.AllAreas)) p = nh.position;
            CreateNote(p, root.transform, papel);
        }

        // Pilhas extras
        Collectible battery = null;
        foreach (Collectible c in Object.FindObjectsByType<Collectible>(FindObjectsSortMode.None))
            if (c.tipo == Collectible.Tipo.Pilha && !c.transform.IsChildOf(root.transform)) { battery = c; break; }
        string[][] batteryRooms =
        {
            new[] { "Office", "Classroom", "Bedroom" },
            new[] { "SeclusionRoomB", "HallUnderground", "MachineRoom" }
        };
        if (battery != null)
        {
            for (int i = 0; i < batteryRooms.Length; i++)
            {
                Vector3 pos; string room;
                if (!FindSpot(batteryRooms[i], used, out pos, out room)) continue;
                GameObject copy = Object.Instantiate(battery.gameObject);
                copy.name = "Pilha extra " + (i + 1);
                copy.transform.SetParent(root.transform, true);
                copy.transform.position = pos;
                Renderer r = copy.GetComponentInChildren<Renderer>();
                if (r != null) copy.transform.position = pos + Vector3.up * (pos.y - r.bounds.min.y + 0.02f);
                report.AppendLine(copy.name + ": " + room + " " + pos);
            }
        }

        Random.state = saved;

        SetupExitDoor();
        SetupDeathAnimation();

        report.AppendLine("Sala da armadilha: " + trapRoom + " | Gerador: " + genRoom);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = root;
        File.WriteAllText("Logs/fase2_montagem.txt", report.ToString());
        Debug.Log("[Fase2] Montagem concluida:\n" + report);
    }

    // ---------- Objetos ----------

    static string CreateTrap(Transform parent, Vector3 enemyPos, HashSet<string> used, Material metal, Material vermelho)
    {
        string[] rooms = { "Surgery", "ExamRoom", "Bedroom", "Classroom", "Office", "DayRoom", "DiningRoom" };
        string chosen = null;
        List<Vector3> pts = null;
        Bounds bounds = new Bounds();

        foreach (string name in rooms)
        {
            Bounds b;
            if (!RoomBounds(name, out b)) continue;
            List<Vector3> p = FloorPoints(b, 150);
            if (p.Count == 0) continue;
            bool reach = Reachable(enemyPos, p[0]);
            report.AppendLine("Candidata a armadilha " + name + ": pontos=" + p.Count + " criatura alcanca=" + reach);
            if (chosen == null || reach)
            {
                chosen = name; pts = p; bounds = b;
                if (reach) break;
            }
        }
        if (chosen == null)
        {
            report.AppendLine("Armadilha: SEM SALA");
            return "?";
        }
        used.Add(chosen);
        GameObject roomObj = GameObject.Find(chosen);

        // Centro: cadeira ou mesa da sala, se existir
        Vector3 center = pts[0];
        Transform door = null;
        foreach (Transform t in roomObj.GetComponentsInChildren<Transform>())
        {
            if (t.name.StartsWith("SurgeryChair") || t.name.StartsWith("SurgeryBed"))
            {
                NavMeshHit ch;
                if (NavMesh.SamplePosition(t.position, out ch, 2.5f, NavMesh.AllAreas)) center = ch.position;
            }
            if (door == null && t.parent == roomObj.transform && t.name.StartsWith("Door")) door = t;
        }

        // Alavanca: ponto da sala mais longe da porta
        Vector3 from = door != null ? door.position : enemyPos;
        Vector3 leverPos = center + Vector3.right * 2.5f;
        float best = -1f;
        foreach (Vector3 p in pts)
        {
            if (Vector3.Distance(p, center) < 1.5f) continue;
            float d = Vector3.Distance(p, from);
            if (d > best) { best = d; leverPos = p; }
        }

        GameObject trap = new GameObject("Armadilha");
        trap.transform.SetParent(parent, true);
        trap.transform.position = leverPos;
        Vector3 look = center - leverPos;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f) trap.transform.rotation = Quaternion.LookRotation(look);

        Part(PrimitiveType.Cube, "Pedestal", trap.transform, new Vector3(0f, 0.55f, 0f), Vector3.zero, new Vector3(0.35f, 1.1f, 0.25f), metal);
        GameObject pivot = new GameObject("Alavanca");
        pivot.transform.SetParent(trap.transform, false);
        pivot.transform.localPosition = new Vector3(0f, 1.05f, 0.13f);
        pivot.transform.localRotation = Quaternion.Euler(-50f, 0f, 0f);
        Part(PrimitiveType.Cube, "Haste", pivot.transform, new Vector3(0f, 0.14f, 0f), Vector3.zero, new Vector3(0.04f, 0.28f, 0.04f), vermelho);
        Part(PrimitiveType.Sphere, "Punho", pivot.transform, new Vector3(0f, 0.3f, 0f), Vector3.zero, new Vector3(0.09f, 0.09f, 0.09f), vermelho);

        BoxCollider box = trap.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.75f, 0f);
        box.size = new Vector3(0.5f, 1.5f, 0.5f);

        GameObject c = new GameObject("Centro");
        c.transform.SetParent(trap.transform, true);
        c.transform.position = center;

        GameObject lightObj = new GameObject("LuzChoque");
        lightObj.transform.SetParent(c.transform, true);
        lightObj.transform.position = center + Vector3.up * 2f;
        Light l = lightObj.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 9f;
        l.intensity = 0.6f;
        l.color = new Color(0.6f, 0.8f, 1f);
        l.shadows = LightShadows.None;

        ShockTrap st = trap.AddComponent<ShockTrap>();
        st.center = c.transform;
        st.shockLight = l;
        st.lever = pivot.transform;
        st.radius = Mathf.Max(4.5f, Vector3.Distance(leverPos, center) + 1.5f);
        st.local = Hint(chosen);

        // Chave: fica escondida ate a criatura morrer
        GameObject key = NewItem("Chave", center, parent, Collectible.Tipo.Chave, new Color(1f, 0.8f, 0.3f));
        Material latao = Mat("Fase2_Latao", new Color(0.8f, 0.62f, 0.25f), 0.9f, 0.6f);
        Part(PrimitiveType.Cube, "Haste", key.transform, new Vector3(0f, 0.01f, 0f), Vector3.zero, new Vector3(0.02f, 0.015f, 0.11f), latao);
        Part(PrimitiveType.Cylinder, "Argola", key.transform, new Vector3(0f, 0.01f, 0.075f), Vector3.zero, new Vector3(0.055f, 0.006f, 0.055f), latao);
        Part(PrimitiveType.Cube, "Dente", key.transform, new Vector3(0.015f, 0.01f, -0.04f), Vector3.zero, new Vector3(0.025f, 0.015f, 0.02f), latao);

        report.AppendLine("Armadilha: " + chosen + " alavanca=" + leverPos + " centro=" + center + " raio=" + st.radius + " criatura alcanca=" + Reachable(enemyPos, center));
        return chosen;
    }

    static string CreateGenerator(Transform parent, Vector3 enemyPos, HashSet<string> used, Material metal, Material painel)
    {
        Vector3 pos; string room;
        if (!FindSpot(new[] { "MachineRoom", "HallUnderground", "Stock", "Morgue" }, used, out pos, out room))
        {
            report.AppendLine("Gerador: SEM LUGAR");
            return "?";
        }

        GameObject gen = new GameObject("Gerador");
        gen.transform.SetParent(parent, true);
        gen.transform.position = pos;

        Part(PrimitiveType.Cube, "Corpo", gen.transform, new Vector3(0f, 0.45f, 0f), Vector3.zero, new Vector3(1.1f, 0.9f, 0.6f), metal);
        Part(PrimitiveType.Cylinder, "Motor", gen.transform, new Vector3(-0.2f, 1.02f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.36f, 0.3f, 0.36f), metal);
        Part(PrimitiveType.Cube, "Quadro", gen.transform, new Vector3(0.3f, 0.6f, 0.31f), Vector3.zero, new Vector3(0.36f, 0.32f, 0.03f), painel);

        BoxCollider box = gen.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.6f, 0f);
        box.size = new Vector3(1.2f, 1.25f, 0.75f);

        GameObject lightObj = new GameObject("LuzStatus");
        lightObj.transform.SetParent(gen.transform, false);
        lightObj.transform.localPosition = new Vector3(0.3f, 0.95f, 0.45f);
        Light l = lightObj.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 3f;
        l.intensity = 2.5f;
        l.color = new Color(1f, 0.15f, 0.1f);
        l.shadows = LightShadows.None;

        Generator g = gen.AddComponent<Generator>();
        g.statusLight = l;
        g.local = Hint(room);

        report.AppendLine("Gerador: " + room + " " + pos + " criatura alcanca=" + Reachable(enemyPos, pos));
        return room;
    }

    static void CreateFuse(int n, Vector3 floor, Transform parent, Material ceramica, Material latao)
    {
        GameObject fuse = NewItem("Fusivel " + n, floor, parent, Collectible.Tipo.Fusivel, new Color(0.6f, 0.85f, 1f));
        Part(PrimitiveType.Cylinder, "Corpo", fuse.transform, new Vector3(0f, 0.035f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.07f, 0.07f, 0.07f), ceramica);
        Part(PrimitiveType.Cylinder, "Ponta A", fuse.transform, new Vector3(0.075f, 0.035f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.076f, 0.012f, 0.076f), latao);
        Part(PrimitiveType.Cylinder, "Ponta B", fuse.transform, new Vector3(-0.075f, 0.035f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.076f, 0.012f, 0.076f), latao);
    }

    static void CreateNote(Vector3 floor, Transform parent, Material papel)
    {
        GameObject note = NewItem("Bilhete", floor, parent, Collectible.Tipo.Bilhete, new Color(1f, 0.85f, 0.55f));
        Part(PrimitiveType.Cube, "Papel", note.transform, new Vector3(0f, 0.004f, 0f), Vector3.zero, new Vector3(0.21f, 0.004f, 0.3f), papel);
        note.GetComponent<Collectible>().textoProntuario =
            "BILHETE RASGADO\n\n" +
            "Hans e Peter levaram a chave do portão na ronda da cozinha.\n" +
            "Eles nunca voltaram. Ele ficou com tudo o que era deles.\n\n" +
            "O gerador ainda funciona, se alguém achar os fusíveis.\n" +
            "O choque foi a única coisa que o fez recuar.\n\n" +
            "[ESC] Fechar";
        report.AppendLine("Bilhete: " + floor);
    }

    // Item coletavel vazio, com colisor e brilho
    static GameObject NewItem(string name, Vector3 floor, Transform parent, Collectible.Tipo tipo, Color glow)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, true);
        go.transform.position = floor + Vector3.up * 0.01f;
        go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        Collectible c = go.AddComponent<Collectible>();
        c.tipo = tipo;

        BoxCollider box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = new Vector3(0f, 0.15f, 0f);
        box.size = new Vector3(0.4f, 0.3f, 0.4f);

        GameObject g = new GameObject("Glow");
        g.transform.SetParent(go.transform, false);
        g.transform.localPosition = new Vector3(0f, 0.4f, 0f);
        Light l = g.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 1.6f;
        l.intensity = 1.2f;
        l.color = glow;
        l.shadows = LightShadows.None;
        return go;
    }

    // Primitiva sem colisor
    static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localEuler, Vector3 scale, Material mat)
    {
        GameObject p = GameObject.CreatePrimitive(type);
        p.name = name;
        Object.DestroyImmediate(p.GetComponent<Collider>());
        p.transform.SetParent(parent, false);
        p.transform.localPosition = localPos;
        p.transform.localEulerAngles = localEuler;
        p.transform.localScale = scale;
        if (mat != null) p.GetComponent<Renderer>().sharedMaterial = mat;
        return p;
    }

    static Material Mat(string name, Color color, float metallic, float smoothness)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        string path = "Assets/Materials/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = color;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------- Porta e animacao ----------

    static void SetupExitDoor()
    {
        GameObject door = GameObject.Find("MainDoor");
        if (door == null)
        {
            report.AppendLine("Porta principal: MainDoor NAO ENCONTRADA");
            return;
        }
        if (door.GetComponent<ExitDoor>() == null) Undo.AddComponent<ExitDoor>(door);
        if (door.GetComponentInChildren<Collider>() == null)
        {
            Renderer r = door.GetComponentInChildren<Renderer>();
            GameObject col = new GameObject("ColisorSaida");
            col.transform.SetParent(door.transform, true);
            col.transform.position = r != null ? r.bounds.center : door.transform.position;
            col.transform.rotation = Quaternion.identity;
            BoxCollider box = col.AddComponent<BoxCollider>();
            Vector3 size = r != null ? r.bounds.size : new Vector3(2f, 2.5f, 0.3f);
            Vector3 ls = col.transform.lossyScale;
            box.size = new Vector3(size.x / Mathf.Max(ls.x, 0.001f), size.y / Mathf.Max(ls.y, 0.001f), size.z / Mathf.Max(ls.z, 0.001f));
        }
        report.AppendLine("Porta principal: ExitDoor em " + door.transform.position);
    }

    static void SetupDeathAnimation()
    {
        AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctrl == null)
        {
            report.AppendLine("Animacao de morte: controller NAO ENCONTRADO");
            return;
        }

        AnimationClip death = null;
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(CreatureModelPath))
        {
            AnimationClip clip = o as AnimationClip;
            if (clip != null && clip.name.Contains("Death") && !clip.name.StartsWith("__preview__")) death = clip;
        }
        if (death == null)
        {
            report.AppendLine("Animacao de morte: clipe NAO ENCONTRADO");
            return;
        }

        bool hasParam = false;
        foreach (AnimatorControllerParameter p in ctrl.parameters)
            if (p.name == "Die") hasParam = true;
        if (!hasParam) ctrl.AddParameter("Die", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        AnimatorState state = null;
        foreach (ChildAnimatorState s in sm.states)
            if (s.state.name == "Death") state = s.state;
        if (state == null)
        {
            state = sm.AddState("Death");
            AnimatorStateTransition tr = sm.AddAnyStateTransition(state);
            tr.AddCondition(AnimatorConditionMode.If, 0f, "Die");
            tr.hasExitTime = false;
            tr.duration = 0.15f;
            tr.canTransitionToSelf = false;
        }
        state.motion = death;

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        report.AppendLine("Animacao de morte: " + death.name + " (" + death.length.ToString("0.00") + " s, loop=" + death.isLooping + ")");
    }

    // ---------- Busca de lugares ----------

    static bool RoomBounds(string name, out Bounds b)
    {
        b = new Bounds();
        GameObject go = GameObject.Find(name);
        if (go == null) return false;
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return false;
        b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return true;
    }

    // Pontos do NavMesh no chao da sala
    static List<Vector3> FloorPoints(Bounds b, int tries)
    {
        List<Vector3> pts = new List<Vector3>();
        Vector3 inset = new Vector3(b.size.x * 0.12f, 0f, b.size.z * 0.12f);
        Vector3 min = b.min + inset, max = b.max - inset;
        for (int i = 0; i < tries; i++)
        {
            Vector3 p = i == 0
                ? new Vector3(b.center.x, b.min.y + 0.5f, b.center.z)
                : new Vector3(Random.Range(min.x, max.x), Random.Range(b.min.y, b.center.y), Random.Range(min.z, max.z));
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(p, out hit, 1.5f, NavMesh.AllAreas)) continue;
            Vector3 h = hit.position;
            if (h.x < min.x || h.x > max.x || h.z < min.z || h.z > max.z) continue;
            if (h.y < b.min.y - 0.5f || h.y > b.center.y) continue;
            pts.Add(h);
        }
        return pts;
    }

    static bool FindSpot(string[] rooms, HashSet<string> used, out Vector3 pos, out string room)
    {
        foreach (string name in rooms)
        {
            if (used.Contains(name)) continue;
            Bounds b;
            if (!RoomBounds(name, out b)) continue;
            List<Vector3> pts = FloorPoints(b, 60);
            if (pts.Count == 0) continue;
            pos = pts[Random.Range(0, pts.Count)];
            room = name;
            used.Add(name);
            return true;
        }
        pos = Vector3.zero;
        room = null;
        return false;
    }

    static bool Reachable(Vector3 from, Vector3 to)
    {
        NavMeshPath path = new NavMeshPath();
        return NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
    }

    static string Hint(string room)
    {
        switch (room)
        {
            case "MachineRoom": return "casa de máquinas, no subsolo";
            case "HallUnderground": return "corredor do subsolo";
            case "Morgue": return "necrotério, no subsolo";
            case "Stock": return "depósito";
            case "Surgery": return "sala de cirurgia, no 2º andar";
            case "ExamRoom": return "sala de exames, no 2º andar";
            case "Bedroom": return "dormitório, no 2º andar";
            case "Classroom": return "sala de aula, no 2º andar";
            case "Office": return "escritório, no 2º andar";
            case "DayRoom": return "sala de convivência";
            case "DiningRoom": return "refeitório";
            default: return room;
        }
    }
}
