using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IVF.Infrastructure.Seeding;

public static class ConceptSeeder
{
    public static async Task SeedConceptsAsync(IServiceProvider services)
    {
        var context = services.GetRequiredService<IvfDbContext>();

        // Skip if already seeded
        if (await context.Concepts.AnyAsync())
        {
            Console.WriteLine("✓ Concepts already seeded");
            return;
        }

        Console.WriteLine("Seeding medical concepts...");

        var concepts = new List<Concept>();

        // === VITAL SIGNS ===
        var bloodPressure = Concept.Create(
            "BP",
            "Blood Pressure",
            "Systolic and diastolic blood pressure measurement",
            "LOCAL",
            ConceptType.Clinical
        );
        bloodPressure.AddMapping("SNOMED CT", "75367002", "Blood pressure (observable entity)", "equivalent");
        bloodPressure.AddMapping("LOINC", "85354-9", "Blood pressure panel", "equivalent");
        concepts.Add(bloodPressure);

        var heartRate = Concept.Create(
            "HR",
            "Heart Rate",
            "Number of heartbeats per minute",
            "LOCAL",
            ConceptType.Clinical
        );
        heartRate.AddMapping("SNOMED CT", "364075005", "Heart rate (observable entity)", "equivalent");
        heartRate.AddMapping("LOINC", "8867-4", "Heart rate", "equivalent");
        concepts.Add(heartRate);

        var temperature = Concept.Create(
            "TEMP",
            "Body Temperature",
            "Core body temperature measurement",
            "LOCAL",
            ConceptType.Clinical
        );
        temperature.AddMapping("SNOMED CT", "386725007", "Body temperature (observable entity)", "equivalent");
        temperature.AddMapping("LOINC", "8310-5", "Body temperature", "equivalent");
        concepts.Add(temperature);

        var weight = Concept.Create(
            "WEIGHT",
            "Body Weight",
            "Total body weight in kilograms",
            "LOCAL",
            ConceptType.Clinical
        );
        weight.AddMapping("SNOMED CT", "27113001", "Body weight (observable entity)", "equivalent");
        weight.AddMapping("LOINC", "29463-7", "Body weight", "equivalent");
        concepts.Add(weight);

        var height = Concept.Create(
            "HEIGHT",
            "Body Height",
            "Standing height in centimeters",
            "LOCAL",
            ConceptType.Anatomical
        );
        height.AddMapping("SNOMED CT", "50373000", "Body height measure (observable entity)", "equivalent");
        height.AddMapping("LOINC", "8302-2", "Body height", "equivalent");
        concepts.Add(height);

        // === BLOOD TYPES ===
        var bloodAPos = Concept.Create(
            "BLOOD_A_POS",
            "Blood Type A+",
            "Blood group A positive",
            "LOCAL",
            ConceptType.Laboratory
        );
        bloodAPos.AddMapping("SNOMED CT", "112144000", "Blood group A Rh(D) positive", "equivalent");
        concepts.Add(bloodAPos);

        var bloodBPos = Concept.Create(
            "BLOOD_B_POS",
            "Blood Type B+",
            "Blood group B positive",
            "LOCAL",
            ConceptType.Laboratory
        );
        bloodBPos.AddMapping("SNOMED CT", "165743006", "Blood group B Rh(D) positive", "equivalent");
        concepts.Add(bloodBPos);

        var bloodOPos = Concept.Create(
            "BLOOD_O_POS",
            "Blood Type O+",
            "Blood group O positive",
            "LOCAL",
            ConceptType.Laboratory
        );
        bloodOPos.AddMapping("SNOMED CT", "278149003", "Blood group O Rh(D) positive", "equivalent");
        concepts.Add(bloodOPos);

        var bloodABPos = Concept.Create(
            "BLOOD_AB_POS",
            "Blood Type AB+",
            "Blood group AB positive",
            "LOCAL",
            ConceptType.Laboratory
        );
        bloodABPos.AddMapping("SNOMED CT", "278152006", "Blood group AB Rh(D) positive", "equivalent");
        concepts.Add(bloodABPos);

        // === IVF LAB TESTS ===
        var fsh = Concept.Create(
            "FSH",
            "Follicle Stimulating Hormone",
            "FSH level measurement for ovarian reserve assessment",
            "LOCAL",
            ConceptType.Laboratory
        );
        fsh.AddMapping("LOINC", "15067-2", "Follitropin [Units/volume] in Serum or Plasma", "equivalent");
        fsh.AddMapping("SNOMED CT", "313881005", "Follicle stimulating hormone measurement", "equivalent");
        concepts.Add(fsh);

        var lh = Concept.Create(
            "LH",
            "Luteinizing Hormone",
            "LH level measurement",
            "LOCAL",
            ConceptType.Laboratory
        );
        lh.AddMapping("LOINC", "10501-5", "Lutropin [Units/volume] in Serum or Plasma", "equivalent");
        lh.AddMapping("SNOMED CT", "69527006", "Luteinizing hormone measurement", "equivalent");
        concepts.Add(lh);

        var estradiol = Concept.Create(
            "E2",
            "Estradiol",
            "Estradiol (E2) hormone level",
            "LOCAL",
            ConceptType.Laboratory
        );
        estradiol.AddMapping("LOINC", "2243-4", "Estradiol (E2) [Mass/volume] in Serum or Plasma", "equivalent");
        estradiol.AddMapping("SNOMED CT", "269827005", "Estradiol measurement", "equivalent");
        concepts.Add(estradiol);

        var amh = Concept.Create(
            "AMH",
            "Anti-Müllerian Hormone",
            "AMH level for ovarian reserve assessment",
            "LOCAL",
            ConceptType.Laboratory
        );
        amh.AddMapping("LOINC", "21198-7", "Anti-Mullerian hormone [Units/volume] in Serum or Plasma", "equivalent");
        amh.AddMapping("SNOMED CT", "445321000124102", "Measurement of anti-Mullerian hormone", "equivalent");
        concepts.Add(amh);

        var progesterone = Concept.Create(
            "PROG",
            "Progesterone",
            "Progesterone hormone level",
            "LOCAL",
            ConceptType.Laboratory
        );
        progesterone.AddMapping("LOINC", "2839-9", "Progesterone [Mass/volume] in Serum or Plasma", "equivalent");
        progesterone.AddMapping("SNOMED CT", "269836004", "Progesterone measurement", "equivalent");
        concepts.Add(progesterone);

        // === SPERM ANALYSIS ===
        var spermCount = Concept.Create(
            "SPERM_COUNT",
            "Sperm Concentration",
            "Sperm concentration in millions per milliliter",
            "LOCAL",
            ConceptType.Laboratory
        );
        spermCount.AddMapping("LOINC", "12841-3", "Spermatozoa [#/volume] in Semen", "equivalent");
        spermCount.AddMapping("SNOMED CT", "250726004", "Sperm density", "equivalent");
        concepts.Add(spermCount);

        var motility = Concept.Create(
            "SPERM_MOTILITY",
            "Sperm Motility",
            "Percentage of motile sperm",
            "LOCAL",
            ConceptType.Laboratory
        );
        motility.AddMapping("LOINC", "19048-8", "Spermatozoa Motile/100 spermatozoa in Semen", "equivalent");
        motility.AddMapping("SNOMED CT", "250729006", "Sperm motility", "equivalent");
        concepts.Add(motility);

        var morphology = Concept.Create(
            "SPERM_MORPHOLOGY",
            "Sperm Morphology",
            "Percentage of normally formed sperm",
            "LOCAL",
            ConceptType.Laboratory
        );
        morphology.AddMapping("LOINC", "19051-2", "Spermatozoa Normal/100 spermatozoa in Semen by Light microscopy", "equivalent");
        morphology.AddMapping("SNOMED CT", "250730001", "Sperm morphology", "equivalent");
        concepts.Add(morphology);

        // === EMBRYOLOGY ===
        var embryoGrade = Concept.Create(
            "EMBRYO_GRADE",
            "Embryo Quality Grade",
            "Embryo morphological quality assessment",
            "LOCAL",
            ConceptType.Clinical
        );
        embryoGrade.AddMapping("SNOMED CT", "445009008", "Assessment of embryo", "related");
        concepts.Add(embryoGrade);

        var blastocystGrade = Concept.Create(
            "BLAST_GRADE",
            "Blastocyst Grade",
            "Blastocyst quality grading (Gardner system)",
            "LOCAL",
            ConceptType.Clinical
        );
        blastocystGrade.AddMapping("SNOMED CT", "445011004", "Blastocyst grading", "related");
        concepts.Add(blastocystGrade);

        // Add all concepts to context
        context.Concepts.AddRange(concepts);
        await context.SaveChangesAsync();

        Console.WriteLine($"✓ Seeded {concepts.Count} medical concepts with SNOMED CT/LOINC mappings");
    }
}
