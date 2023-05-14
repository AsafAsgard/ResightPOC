using UnityEngine;
using UnityEditor;
using System;
using System.Linq;

[InitializeOnLoad]
public class ResightSplashScreen
{
    static ResightSplashScreen()
    {
        var currentLogos = PlayerSettings.SplashScreen.logos;
        if (currentLogos == null || currentLogos.Length == 0)
        {
            currentLogos = new PlayerSettings.SplashScreenLogo[1];
            currentLogos[0] = default;
            PlayerSettings.SplashScreen.logos = currentLogos;
        }

        var splashScreenLogo = Array.Find(currentLogos, (pLogo) => pLogo.logo && pLogo.logo.name.Contains("Resight"));
        if (!splashScreenLogo.Equals(default(PlayerSettings.SplashScreenLogo)))
        {
            return;
        }

        if (currentLogos.Length == 1 && currentLogos[0].Equals(default(PlayerSettings.SplashScreenLogo)))
        {
            currentLogos[0] = PlayerSettings.SplashScreenLogo.CreateWithUnityLogo();
        }

        PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Dolly;
        PlayerSettings.SplashScreen.drawMode = PlayerSettings.SplashScreen.DrawMode.AllSequential;

        var logos = currentLogos.ToList();

        // Company logo
        Sprite companyLogo = (Sprite)AssetDatabase.LoadAssetAtPath("Packages/io.resight.sdk/Assets/Textures/PoweredByResightWhite.png", typeof(Sprite));
        logos.Insert(0, PlayerSettings.SplashScreenLogo.Create(4f, companyLogo));

        PlayerSettings.SplashScreen.logos = logos.ToArray();
    }
}
