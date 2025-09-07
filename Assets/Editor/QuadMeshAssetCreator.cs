using UnityEditor;
using UnityEngine;

public static class QuadMeshAssetCreator
{
	[MenuItem("Assets/Create/Mesh/Quad 1x1.7 (XY)")]
	public static void CreateQuadXY()
	{
		CreateQuadAsset(1f, 1.7f, true);
	}

	[MenuItem("Assets/Create/Mesh/Quad 1x1.7 (XZ)")]
	public static void CreateQuadXZ()
	{
		CreateQuadAsset(1f, 1.7f, false);
	}

	[MenuItem("Assets/Create/Mesh/Quad 1.3x1.7 (XZ)")]
	public static void CreateBackQuad()
	{
		CreateQuadAsset(1.3f, 1.7f, true);
	}

	private static void CreateQuadAsset(float width, float height, bool onXYPlane)
	{
		var mesh = CreateQuadMesh(width, height, onXYPlane);

		string targetFolder = GetSelectedFolderPath();
		string nameSuffix = onXYPlane ? "XY" : "XZ";
		string basePath = System.IO.Path.Combine(targetFolder, $"Quad_{width}x{height}_{nameSuffix}.asset").Replace('\\', '/');
		string uniquePath = AssetDatabase.GenerateUniqueAssetPath(basePath);

		AssetDatabase.CreateAsset(mesh, uniquePath);
		AssetDatabase.SaveAssets();
		EditorUtility.FocusProjectWindow();
		Selection.activeObject = mesh;
	}

	private static Mesh CreateQuadMesh(float width, float height, bool onXYPlane)
	{
		var mesh = new Mesh { name = $"Quad_{width}x{height}" };
		float halfWidth = width * 0.5f;
		float halfHeight = height * 0.5f;

		Vector3[] vertices = onXYPlane
			? new[]
			{
				new Vector3(-halfWidth, -halfHeight, 0f),
				new Vector3(-halfWidth,  halfHeight, 0f),
				new Vector3( halfWidth, -halfHeight, 0f),
				new Vector3( halfWidth,  halfHeight, 0f),
			}
			: new[]
			{
				new Vector3(-halfWidth, 0f, -halfHeight),
				new Vector3(-halfWidth, 0f,  halfHeight),
				new Vector3( halfWidth, 0f, -halfHeight),
				new Vector3( halfWidth, 0f,  halfHeight),
			};

		int[] triangles = { 0, 1, 2, 2, 1, 3 };
		Vector2[] uvs =
		{
			new Vector2(0f, 0f),
			new Vector2(0f, 1f),
			new Vector2(1f, 0f),
			new Vector2(1f, 1f)
		};
		Vector3 normal = onXYPlane ? Vector3.forward : Vector3.up;
		Vector3[] normals = { normal, normal, normal, normal };

		mesh.SetVertices(vertices);
		mesh.SetTriangles(triangles, 0);
		mesh.SetNormals(normals);
		mesh.SetUVs(0, uvs);
		mesh.RecalculateBounds();
		return mesh;
	}

	private static string GetSelectedFolderPath()
	{
		string path = "Assets";
		var selection = Selection.GetFiltered(typeof(Object), SelectionMode.Assets);
		if (selection != null && selection.Length > 0)
		{
			string selectedPath = AssetDatabase.GetAssetPath(selection[0]);
			if (!string.IsNullOrEmpty(selectedPath))
			{
				if (System.IO.File.Exists(selectedPath))
				{
					path = System.IO.Path.GetDirectoryName(selectedPath)?.Replace('\\', '/') ?? "Assets";
				}
				else
				{
					path = selectedPath;
				}
			}
		}
		return path;
	}
}


