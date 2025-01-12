﻿using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Dalamud.Game;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Logging;
using Dalamud.Utility;
using Dalamud.Utility.Signatures;
using Lumina.Excel.GeneratedSheets;

namespace WhereAmIAgain;

[StructLayout(LayoutKind.Explicit, Size = 76)]
public readonly struct TerritoryInfoStruct
{
    [FieldOffset(8)] private readonly int InSanctuary;
    [FieldOffset(16)] public readonly uint RegionID;
    [FieldOffset(20)] public readonly uint SubAreaID;

    public bool IsInSanctuary => InSanctuary == 1;
    public PlaceName? Region => Service.DataManager.GetExcelSheet<PlaceName>()!.GetRow(RegionID);
    public PlaceName? SubArea => Service.DataManager.GetExcelSheet<PlaceName>()!.GetRow(SubAreaID);
}

public unsafe class DtrDisplay : IDisposable
{
    private Configuration Config => Service.Configuration;

    private PlaceName? currentContinent;
    private PlaceName? currentTerritory;
    private PlaceName? currentRegion;
    private PlaceName? currentSubArea;

    private uint lastTerritory;
    private uint lastRegion;
    private uint lastSubArea;

    private readonly DtrBarEntry dtrEntry;

    private bool locationChanged;
    
    [Signature("8B 2D ?? ?? ?? ?? 41 BF", ScanType = ScanType.StaticAddress)]
    private readonly TerritoryInfoStruct* territoryInfo = null!;

    [Signature("48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 80 BD", ScanType = ScanType.StaticAddress)]
    private readonly int* instanceNumber = null!;

    public DtrDisplay()
    {
        SignatureHelper.Initialise(this);

        dtrEntry = Service.DtrBar.Get("Where am I again?");
        
        Service.Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        Service.Framework.Update -= OnFrameworkUpdate;
        
        dtrEntry.Dispose();
    }

    private void OnFrameworkUpdate(Framework framework)
    {
        if (Service.ClientState.LocalPlayer is null) return;

        UpdateRegion();

        UpdateSubArea();
        
        UpdateTerritory();
        
        if (locationChanged)
        {
            UpdateDtrText();
        }
    }

    public void UpdateDtrText()
    {
        try
        {
            var formattedString = Config.FormatString.Format(
                currentContinent?.Name.RawString ?? string.Empty,
                currentTerritory?.Name.RawString ?? string.Empty,
                currentRegion?.Name.RawString ?? string.Empty,
                currentSubArea?.Name.RawString ?? string.Empty);

            var filteredString = Regex.Replace(formattedString, "^[^\\p{L}\\p{N}]*|[^\\p{L}\\p{N}]*$", "");

            if (Config.ShowInstanceNumber)
            {
                filteredString += GetCharacterForInstanceNumber(GetInstanceNumber());
            }
            
            dtrEntry.Text = new SeStringBuilder().AddText(filteredString).BuiltString;
            locationChanged = false;
        }
        catch(FormatException)
        {
            // Ignore format exceptions, because we warn the user in the config window if they are missing a format symbol 
        }
    }

    private int GetInstanceNumber() => *(int*) ((byte*) instanceNumber + 32);

    private string GetCharacterForInstanceNumber(int instance)
    {
        if (instance == 0) return string.Empty;
        
        return $" {((SeIconChar)((int)SeIconChar.Instance1 + (instance - 1))).ToIconChar()}";
    }

    private void UpdateTerritory()
    {
        if (lastTerritory != Service.ClientState.TerritoryType)
        {
            lastTerritory = Service.ClientState.TerritoryType;
            var territory = Service.DataManager.GetExcelSheet<TerritoryType>()!
                .GetRow(Service.ClientState.TerritoryType);

            currentTerritory = territory?.PlaceName.Value;
            currentContinent = territory?.PlaceNameRegion.Value;
            locationChanged = true;
        }
    }

    private void UpdateSubArea()
    {
        if (lastSubArea != territoryInfo->SubAreaID)
        {
            lastSubArea = territoryInfo->SubAreaID;
            currentSubArea = territoryInfo->SubArea;
            locationChanged = true;
        }
    }

    private void UpdateRegion()
    {
        if (lastRegion != territoryInfo->RegionID)
        {
            lastRegion = territoryInfo->RegionID;
            currentRegion = territoryInfo->Region;
            locationChanged = true;
        }
    }
}