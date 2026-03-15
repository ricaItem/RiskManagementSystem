using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data;
using WEB_Sentro.Data.Entities;

namespace WEB_Sentro.Data.Seed
{
    public static class CostCodeSeeder
    {
        public static async Task SeedAsync(TenantDbContext context, int orgId)
        {
            if (await context.CostCodes.AnyAsync(c => c.OrgId == orgId))
            {
                // Already seeded
                return;
            }

            // --- Level 1: Divisions ---
            var divisions = new List<CostCode>
            {
                new() { OrgId = orgId, Code = "00-000", Description = "Procurement and Contracting Requirements" },
                new() { OrgId = orgId, Code = "01-000", Description = "General Requirements" },
                new() { OrgId = orgId, Code = "02-000", Description = "Existing Conditions" },
                new() { OrgId = orgId, Code = "03-000", Description = "Concrete" },
                new() { OrgId = orgId, Code = "04-000", Description = "Masonry" },
                new() { OrgId = orgId, Code = "05-000", Description = "Metals" },
                new() { OrgId = orgId, Code = "06-000", Description = "Wood, Plastics, and Composites" },
                new() { OrgId = orgId, Code = "07-000", Description = "Thermal and Moisture Protection" },
                new() { OrgId = orgId, Code = "08-000", Description = "Openings" },
                new() { OrgId = orgId, Code = "09-000", Description = "Finishes" },
                new() { OrgId = orgId, Code = "10-000", Description = "Specialties" },
                new() { OrgId = orgId, Code = "11-000", Description = "Equipment" },
                new() { OrgId = orgId, Code = "12-000", Description = "Furnishings" },
                new() { OrgId = orgId, Code = "13-000", Description = "Special Construction" },
                new() { OrgId = orgId, Code = "14-000", Description = "Conveying Equipment" },
                new() { OrgId = orgId, Code = "21-000", Description = "Fire Suppression" },
                new() { OrgId = orgId, Code = "22-000", Description = "Plumbing" },
                new() { OrgId = orgId, Code = "23-000", Description = "Heating, Ventilating, and Air Conditioning (HVAC)" },
                new() { OrgId = orgId, Code = "26-000", Description = "Electrical" },
                new() { OrgId = orgId, Code = "27-000", Description = "Communications" },
                new() { OrgId = orgId, Code = "28-000", Description = "Electronic Safety and Security" },
                new() { OrgId = orgId, Code = "31-000", Description = "Earthwork" },
                new() { OrgId = orgId, Code = "32-000", Description = "Exterior Improvements" },
                new() { OrgId = orgId, Code = "33-000", Description = "Utilities" }
            };

            foreach (var div in divisions)
            {
                div.CreatedAt = DateTime.UtcNow;
                div.UpdatedAt = DateTime.UtcNow;
                context.CostCodes.Add(div);
            }
            await context.SaveChangesAsync(); // Save to get IDs

            // --- Level 2: Sub-Codes ---
            // We define helper variable for parent lookup
            CostCode GetParent(string code) => divisions.First(d => d.Code == code);

            var subCodes = new List<CostCode>
            {
                // 00 - Procurement
                new() { OrgId = orgId, Code = "00-100", Description = "Bid Requirements", ParentCostCodeId = GetParent("00-000").CostCodeId },
                
                // 01 - General
                new() { OrgId = orgId, Code = "01-100", Description = "Summary", ParentCostCodeId = GetParent("01-000").CostCodeId },
                new() { OrgId = orgId, Code = "01-200", Description = "Price and Payment Procedures", ParentCostCodeId = GetParent("01-000").CostCodeId },

                // 02 - Existing Conditions
                new() { OrgId = orgId, Code = "02-400", Description = "Demolition and Structure Moving", ParentCostCodeId = GetParent("02-000").CostCodeId },

                // 03 - Concrete
                new() { OrgId = orgId, Code = "03-100", Description = "Concrete Forming and Accessories", ParentCostCodeId = GetParent("03-000").CostCodeId },
                new() { OrgId = orgId, Code = "03-200", Description = "Concrete Reinforcing", ParentCostCodeId = GetParent("03-000").CostCodeId },
                new() { OrgId = orgId, Code = "03-300", Description = "Cast-in-Place Concrete", ParentCostCodeId = GetParent("03-000").CostCodeId },
                new() { OrgId = orgId, Code = "03-400", Description = "Precast Concrete", ParentCostCodeId = GetParent("03-000").CostCodeId },

                // 04 - Masonry
                new() { OrgId = orgId, Code = "04-200", Description = "Unit Masonry", ParentCostCodeId = GetParent("04-000").CostCodeId },

                // 05 - Metals
                new() { OrgId = orgId, Code = "05-100", Description = "Structural Metal Framing", ParentCostCodeId = GetParent("05-000").CostCodeId },
                new() { OrgId = orgId, Code = "05-500", Description = "Metal Fabrications", ParentCostCodeId = GetParent("05-000").CostCodeId },

                // 06 - Wood
                new() { OrgId = orgId, Code = "06-100", Description = "Rough Carpentry", ParentCostCodeId = GetParent("06-000").CostCodeId },
                new() { OrgId = orgId, Code = "06-200", Description = "Finish Carpentry", ParentCostCodeId = GetParent("06-000").CostCodeId },

                // 07 - Thermal
                new() { OrgId = orgId, Code = "07-200", Description = "Thermal Protection", ParentCostCodeId = GetParent("07-000").CostCodeId },
                new() { OrgId = orgId, Code = "07-900", Description = "Joint Protection", ParentCostCodeId = GetParent("07-000").CostCodeId },

                // 08 - Openings
                new() { OrgId = orgId, Code = "08-100", Description = "Doors and Frames", ParentCostCodeId = GetParent("08-000").CostCodeId },
                new() { OrgId = orgId, Code = "08-500", Description = "Windows", ParentCostCodeId = GetParent("08-000").CostCodeId },

                // 09 - Finishes
                new() { OrgId = orgId, Code = "09-200", Description = "Plaster and Gypsum Board", ParentCostCodeId = GetParent("09-000").CostCodeId },
                new() { OrgId = orgId, Code = "09-300", Description = "Tiling", ParentCostCodeId = GetParent("09-000").CostCodeId },
                new() { OrgId = orgId, Code = "09-900", Description = "Painting and Coating", ParentCostCodeId = GetParent("09-000").CostCodeId },

                // 10 - Specialties
                new() { OrgId = orgId, Code = "10-100", Description = "Visual Display Units", ParentCostCodeId = GetParent("10-000").CostCodeId },

                // 11 - Equipment
                new() { OrgId = orgId, Code = "11-300", Description = "Residential Equipment", ParentCostCodeId = GetParent("11-000").CostCodeId },

                // 12 - Furnishings
                new() { OrgId = orgId, Code = "12-300", Description = "Casework", ParentCostCodeId = GetParent("12-000").CostCodeId },

                // 13 - Special Construction
                new() { OrgId = orgId, Code = "13-300", Description = "Special Structures", ParentCostCodeId = GetParent("13-000").CostCodeId },

                // 14 - Conveying
                new() { OrgId = orgId, Code = "14-200", Description = "Elevators", ParentCostCodeId = GetParent("14-000").CostCodeId },

                // 21 - Fire Suppression
                new() { OrgId = orgId, Code = "21-100", Description = "Water-Based Fire-Suppression Systems", ParentCostCodeId = GetParent("21-000").CostCodeId },

                // 22 - Plumbing
                new() { OrgId = orgId, Code = "22-100", Description = "Plumbing Piping", ParentCostCodeId = GetParent("22-000").CostCodeId },

                // 23 - HVAC
                new() { OrgId = orgId, Code = "23-300", Description = "HVAC Air Distribution", ParentCostCodeId = GetParent("23-000").CostCodeId },

                // 26 - Electrical
                new() { OrgId = orgId, Code = "26-200", Description = "Low-Voltage Electrical Transmission", ParentCostCodeId = GetParent("26-000").CostCodeId },
                new() { OrgId = orgId, Code = "26-500", Description = "Lighting", ParentCostCodeId = GetParent("26-000").CostCodeId },

                // 27 - Communications
                new() { OrgId = orgId, Code = "27-100", Description = "Structured Cabling", ParentCostCodeId = GetParent("27-000").CostCodeId },

                // 28 - Electronic Safety
                new() { OrgId = orgId, Code = "28-100", Description = "Access Control", ParentCostCodeId = GetParent("28-000").CostCodeId },

                // 31 - Earthwork
                new() { OrgId = orgId, Code = "31-100", Description = "Site Clearing", ParentCostCodeId = GetParent("31-000").CostCodeId },
                new() { OrgId = orgId, Code = "31-200", Description = "Earth Moving", ParentCostCodeId = GetParent("31-000").CostCodeId },
                new() { OrgId = orgId, Code = "31-220", Description = "Grading", ParentCostCodeId = GetParent("31-000").CostCodeId },
                new() { OrgId = orgId, Code = "31-230", Description = "Excavation and Fill", ParentCostCodeId = GetParent("31-000").CostCodeId },
                new() { OrgId = orgId, Code = "31-600", Description = "Special Foundations and Load-Bearing Elements", ParentCostCodeId = GetParent("31-000").CostCodeId },

                // 32 - Exterior Improvements
                new() { OrgId = orgId, Code = "32-100", Description = "Bases, Ballasts, and Paving", ParentCostCodeId = GetParent("32-000").CostCodeId },

                // 33 - Utilities
                new() { OrgId = orgId, Code = "33-100", Description = "Water Utilities", ParentCostCodeId = GetParent("33-000").CostCodeId }
            };

            foreach (var sub in subCodes)
            {
                sub.CreatedAt = DateTime.UtcNow;
                sub.UpdatedAt = DateTime.UtcNow;
                context.CostCodes.Add(sub);
            }

            await context.SaveChangesAsync();
        }
    }
}
