using IVF.Domain.Common;
using IVF.Domain.Enums;

namespace IVF.Domain.Entities;

/// <summary>
/// Semen analysis record for andrology department
/// </summary>
public class SemenAnalysis : BaseEntity
{
    public Guid PatientId { get; private set; }
    public Guid? CycleId { get; private set; }
    public DateTime AnalysisDate { get; private set; }
    public AnalysisType AnalysisType { get; private set; }
    
    // Macroscopic
    public decimal? Volume { get; private set; } // ml
    public string? Appearance { get; private set; }
    public string? Liquefaction { get; private set; }
    public decimal? Ph { get; private set; }
    
    // Microscopic
    public decimal? Concentration { get; private set; } // million/ml
    public decimal? TotalCount { get; private set; } // million
    public decimal? ProgressiveMotility { get; private set; } // %
    public decimal? NonProgressiveMotility { get; private set; } // %
    public decimal? Immotile { get; private set; } // %
    public decimal? NormalMorphology { get; private set; } // %
    public decimal? Vitality { get; private set; } // %
    
    // Post-wash (if applicable)
    public decimal? PostWashConcentration { get; private set; }
    public decimal? PostWashMotility { get; private set; }
    
    public string? Notes { get; private set; }
    public Guid? PerformedByUserId { get; private set; }
    
    // Navigation
    public Patient Patient { get; private set; } = null!;
    public TreatmentCycle? Cycle { get; private set; }

    private SemenAnalysis() { }

    public static SemenAnalysis Create(
        Guid patientId,
        DateTime analysisDate,
        AnalysisType analysisType,
        Guid? cycleId = null,
        Guid? performedByUserId = null)
    {
        return new SemenAnalysis
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            CycleId = cycleId,
            AnalysisDate = analysisDate,
            AnalysisType = analysisType,
            PerformedByUserId = performedByUserId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void RecordMacroscopic(decimal? volume, string? appearance, string? liquefaction, decimal? ph)
    {
        Volume = volume;
        Appearance = appearance;
        Liquefaction = liquefaction;
        Ph = ph;
        SetUpdated();
    }

    public void RecordMicroscopic(
        decimal? concentration,
        decimal? totalCount,
        decimal? progressiveMotility,
        decimal? nonProgressiveMotility,
        decimal? immotile,
        decimal? normalMorphology,
        decimal? vitality)
    {
        Concentration = concentration;
        TotalCount = totalCount;
        ProgressiveMotility = progressiveMotility;
        NonProgressiveMotility = nonProgressiveMotility;
        Immotile = immotile;
        NormalMorphology = normalMorphology;
        Vitality = vitality;
        SetUpdated();
    }

    public void RecordPostWash(decimal? concentration, decimal? motility)
    {
        PostWashConcentration = concentration;
        PostWashMotility = motility;
        SetUpdated();
    }

    public void AddNotes(string notes)
    {
        Notes = notes;
        SetUpdated();
    }
}
