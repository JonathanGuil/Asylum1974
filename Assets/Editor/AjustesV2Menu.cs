using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Menu Asylum: ajustes da V2 e novo NavMesh
public static class AjustesV2Menu
{
    const string ReportFile = "Logs/navmesh_report.txt";

    [MenuItem("Asylum/Aplicar ajustes V2")]
    public static void Aplicar()
    {
        StringBuilder report = new StringBuilder();
        SetAgentRadius(0.35f, report);
        PrepareDoors(report);
        SetValues(report);
        Bake(report);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Finish(report);
    }

    [MenuItem("Asylum/Conferir NavMesh")]
    public static void Conferir()
    {
        StringBuilder report = new StringBuilder();
        CheckPaths(report);
        Finish(report);
    }

    static void Finish(StringBuilder report)
    {
        File.WriteAllText(ReportFile, report.ToString());
        Debug.Log("[AjustesV2]\n" + report);
    }

    // Raio do agente usado no bake
    static void SetAgentRadius(float radius, StringBuilder report)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset");
        if (assets.Length == 0) { report.AppendLine("Raio do agente: NavMeshAreas.asset nao encontrado"); return; }
        SerializedObject so = new SerializedObject(assets[0]);
        SerializedProperty settings = so.FindProperty("m_Settings");
        if (settings == null || settings.arraySize == 0) { report.AppendLine("Raio do agente: sem m_Settings"); return; }
        SerializedProperty r = settings.GetArrayElementAtIndex(0).FindPropertyRelative("agentRadius");
        float old = r.floatValue;
        r.floatValue = radius;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        report.AppendLine("Raio do agente: " + old + " -> " + NavMesh.GetSettingsByID(0).agentRadius);
    }

    // Tira as portas do bake e desliga o recorte
    static void PrepareDoors(StringBuilder report)
    {
        int modifiers = 0, obstacles = 0;
        Door[] doors = Object.FindObjectsByType<Door>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Door door in doors)
        {
            NavMeshModifier mod = door.GetComponent<NavMeshModifier>();
            if (mod == null) { mod = door.gameObject.AddComponent<NavMeshModifier>(); modifiers++; }
            mod.ignoreFromBuild = true;
            mod.applyToChildren = true;
            EditorUtility.SetDirty(mod);

            foreach (NavMeshObstacle o in door.GetComponentsInChildren<NavMeshObstacle>(true))
            {
                if (!o.enabled) continue;
                o.enabled = false;
                obstacles++;
                EditorUtility.SetDirty(o);
            }
        }
        report.AppendLine("Portas: " + doors.Length + ", modificadores novos: " + modifiers + ", obstaculos desligados: " + obstacles);
    }

    // Valores gravados na cena
    static void SetValues(StringBuilder report)
    {
        EnemyAI enemy = Object.FindFirstObjectByType<EnemyAI>();
        if (enemy != null)
        {
            enemy.huntSpeed = 4.8f;
            EditorUtility.SetDirty(enemy);
            NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
            if (agent != null) { agent.radius = 0.35f; EditorUtility.SetDirty(agent); }
        }
        GameFlow flow = Object.FindFirstObjectByType<GameFlow>();
        if (flow != null)
        {
            flow.brilhoDasLampadas = 0.85f;
            EditorUtility.SetDirty(flow);
        }
        report.AppendLine("Valores: huntSpeed 4.8, raio da criatura 0.35, brilhoDasLampadas 0.85");
    }

    // Refaz o NavMesh e grava no mesmo asset
    static void Bake(StringBuilder report)
    {
        NavMeshSurface surface = Object.FindFirstObjectByType<NavMeshSurface>();
        if (surface == null) { report.AppendLine("Bake: sem NavMeshSurface"); return; }

        string path = surface.navMeshData != null ? AssetDatabase.GetAssetPath(surface.navMeshData) : "";
        if (string.IsNullOrEmpty(path)) path = "Assets/Scenes/Jogo/NavMesh-NavMesh Surface.asset";

        double t0 = EditorApplication.timeSinceStartup;
        surface.BuildNavMesh();
        AssetDatabase.CreateAsset(surface.navMeshData, path);
        EditorUtility.SetDirty(surface);
        AssetDatabase.SaveAssets();
        report.AppendLine("Bake: " + (EditorApplication.timeSinceStartup - t0).ToString("0.0") + " s -> " + path);

        CheckPaths(report);
    }

    // Lista o que a criatura alcanca
    static void CheckPaths(StringBuilder report)
    {
        EnemyAI enemy = Object.FindFirstObjectByType<EnemyAI>();
        if (enemy == null) { report.AppendLine("Sem criatura"); return; }
        Vector3 from = enemy.transform.position;

        List<KeyValuePair<string, Vector3>> targets = new List<KeyValuePair<string, Vector3>>();
        PlayerController pc = Object.FindFirstObjectByType<PlayerController>();
        if (pc != null) targets.Add(new KeyValuePair<string, Vector3>("Jogador (inicio)", pc.transform.position));
        foreach (Collectible c in Object.FindObjectsByType<Collectible>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            targets.Add(new KeyValuePair<string, Vector3>(c.name, c.transform.position));
        Generator g = Object.FindFirstObjectByType<Generator>(FindObjectsInactive.Include);
        if (g != null) targets.Add(new KeyValuePair<string, Vector3>("Gerador", g.transform.position));
        ShockTrap trap = Object.FindFirstObjectByType<ShockTrap>(FindObjectsInactive.Include);
        if (trap != null)
        {
            targets.Add(new KeyValuePair<string, Vector3>("Armadilha (centro)", trap.Center));
            targets.Add(new KeyValuePair<string, Vector3>("Armadilha (alavanca)", trap.transform.position));
        }
        ExitDoor exit = Object.FindFirstObjectByType<ExitDoor>(FindObjectsInactive.Include);
        if (exit != null) targets.Add(new KeyValuePair<string, Vector3>("Porta de saida", exit.transform.position));
        foreach (Door door in Object.FindObjectsByType<Door>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            targets.Add(new KeyValuePair<string, Vector3>("Porta " + (door.transform.parent != null ? door.transform.parent.name : door.name), door.transform.position));

        targets.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
        int ok = 0;
        NavMeshPath path = new NavMeshPath();
        foreach (KeyValuePair<string, Vector3> t in targets)
        {
            NavMeshHit hit;
            string result;
            if (!NavMesh.SamplePosition(t.Value, out hit, 2.5f, NavMesh.AllAreas)) result = "FORA DO NAVMESH";
            else if (NavMesh.CalculatePath(from, hit.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete) { result = "ok"; ok++; }
            else result = "SEM CAMINHO";
            report.AppendLine(result + "  " + t.Key + " " + t.Value);
        }
        report.AppendLine("Alcanca " + ok + " de " + targets.Count);
    }
}
