using System;
using System.Collections.Generic;

namespace AIHousingAssistant.Domain.Entities { 

public partial class HousingUnit
{
    public int Id { get; set; }

    public string UnitType { get; set; } = null!;

    public bool IsAvailable { get; set; }
}
}