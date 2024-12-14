using System;
using System.Collections.Generic;
using UnityEngine;

public class PoolManager : MonoBehaviour
{
    public Transform poolParent;
    public GameObject subMeshPrefab;
    public int initialPoolSize = 100;

    Queue<GameObject> pool = new Queue<GameObject>();

    void Awake()
    {
        for (int i = 0; i < initialPoolSize; i++)
        {
            GameObject subMesh = Instantiate(subMeshPrefab, poolParent);
            subMesh.SetActive(false);
            pool.Enqueue(subMesh);
        }
    }

    public GameObject GetSubMeshObject()
    {
        if (pool.Count > 0)
        {
            GameObject subMeshObj = pool.Dequeue();
            subMeshObj.SetActive(true);
            return subMeshObj;
        }
        else
        {
            GameObject newObj = Instantiate(subMeshPrefab, poolParent);
            newObj.SetActive(true);
            Debug.LogWarning("poolManager created a new instance");
            return newObj;
        }
    }

    public void ReturnSubMeshObject(GameObject obj)
    {
        obj.SetActive(false);
        pool.Enqueue(obj);
    }
}
