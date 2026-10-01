# Kaynak: Themes/directory-management-logo.jfif -> PNG (pencere) + cok boyutlu ICO (exe)
param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
# PowerShell 7 yalnizca System.Drawing facade'ini gorur; Bitmap System.Drawing.Common icindedir.
$drawingAssembly = [System.Drawing.Bitmap].Assembly.Location
if ([string]::IsNullOrWhiteSpace($drawingAssembly)) {
    throw "System.Drawing derlemesi bulunamadi."
}

$themes = Join-Path $ProjectRoot 'Themes'
$source = Join-Path $themes 'directory-management-logo.jfif'
if (-not (Test-Path $source)) {
    $source = Join-Path $themes 'directory-management-logo.png'
}
if (-not (Test-Path $source)) {
    throw "Logo kaynagi bulunamadi (jfif veya png): $themes"
}

$pngOut = Join-Path $themes 'directory-management-logo.png'
$icoOut = Join-Path $themes 'directory-management-logo.ico'

Add-Type -ReferencedAssemblies $drawingAssembly @"
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

public static class AppIconWriter
{
    public static void SavePng(Bitmap source, string path, int size)
    {
        using (Bitmap bmp = new Bitmap(size, size))
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            g.DrawImage(source, 0, 0, size, size);
            bmp.Save(path, ImageFormat.Png);
        }
    }

    public static void SaveIco(Bitmap source, string path)
    {
        int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
        List<byte[]> pngChunks = new List<byte[]>();
        foreach (int size in sizes)
        {
            byte[] chunk;
            using (Bitmap bmp = new Bitmap(size, size))
            using (Graphics g = Graphics.FromImage(bmp))
            using (MemoryStream ms = new MemoryStream())
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.DrawImage(source, 0, 0, size, size);
                bmp.Save(ms, ImageFormat.Png);
                chunk = ms.ToArray();
            }
            pngChunks.Add(chunk);
        }

        using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
        using (BinaryWriter bw = new BinaryWriter(fs))
        {
            bw.Write((short)0);
            bw.Write((short)1);
            bw.Write((short)pngChunks.Count);
            int offset = 6 + 16 * pngChunks.Count;
            for (int i = 0; i < sizes.Length; i++)
            {
                int size = sizes[i];
                bw.Write((byte)size);
                bw.Write((byte)size);
                bw.Write((byte)0);
                bw.Write((byte)0);
                bw.Write((short)1);
                bw.Write((short)32);
                bw.Write(pngChunks[i].Length);
                bw.Write(offset);
                offset += pngChunks[i].Length;
            }
            foreach (byte[] chunk in pngChunks)
            {
                bw.Write(chunk);
            }
        }
    }
}
"@

$bitmap = [System.Drawing.Bitmap]::FromFile($source)
try {
    [AppIconWriter]::SavePng($bitmap, $pngOut, 256)
    [AppIconWriter]::SaveIco($bitmap, $icoOut)
    Write-Host "Guncellendi: $pngOut"
    Write-Host "Guncellendi: $icoOut"
}
finally {
    $bitmap.Dispose()
}
