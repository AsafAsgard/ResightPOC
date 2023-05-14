/*
===================================================================
Unity Assets by Resight: https://resight.io
===================================================================
*/

using System;
using UnityEngine;
using Resight.Persistence;

public class RandomColorEntity : MonoBehaviour
{
    private byte[] Encode()
    {
        var color = GetComponent<Renderer>().material.color;
        var rgb = new float[3];
        byte[] bytes = new byte[3 * sizeof(float)];
        rgb[0] = color.r;
        rgb[1] = color.g;
        rgb[2] = color.b;

        int i = 0;

        foreach (var element in rgb)
        {
            var raw = BitConverter.GetBytes(element);
            Array.ConstrainedCopy(raw, 0, bytes, i, raw.Length);
            i += raw.Length;
        }

        return bytes;
    }

    private void Decode(byte[] data)
    {

        var rgb = new float[3];

        for (int i = 0; i < rgb.Length; i++)
        {
            rgb[i] = BitConverter.ToSingle(data, i * sizeof(float));
        }

        GetComponent<Renderer>().material.color = new Color(rgb[0], rgb[1], rgb[2]);
    }

    private void Awake()
    {
        var r = UnityEngine.Random.Range(0, 1.0f);
        var g = UnityEngine.Random.Range(0, 1.0f);
        var b = UnityEngine.Random.Range(0, 1.0f);

        GetComponent<Renderer>().material.color = new Color(r, g, b);
    }

    // Start is called before the first frame update
    void Start()
    {
        var snapped = GetComponent<SnappedObject>();

        // Check if this is a network-spawned object which has an aux data
        if (snapped.AuxData != null)
        {
            Decode(snapped.AuxData);
            return;
        }

        // This is a local, new object, so publish its propertis to the engine via AuxData
        snapped.AuxData = Encode();
    }
}
