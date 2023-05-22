using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public struct Banner
{
    public GameObject prefab;
    public Button button;
}

public class BannerSpawner : MonoBehaviour
{
    [SerializeField]
    private List<Banner> _banners;

    [SerializeField]
    private Transform _cursorTrn;

    private void Awake()
    {
        foreach (var banner in _banners)
        {
            banner.button.onClick.AddListener(delegate { SpawnBanner(banner.prefab); } );
        }
    }

    private void OnDestroy()
    {
        foreach (var banner in _banners)
        {
            banner.button.onClick.RemoveAllListeners();
        }
    }

    private void SpawnBanner(GameObject prefab)
    {
        Instantiate(prefab, _cursorTrn.position, _cursorTrn.rotation);
    }
}
