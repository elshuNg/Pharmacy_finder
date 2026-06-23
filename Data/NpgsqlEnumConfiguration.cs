using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using PharmacyFinder.API.Models;

namespace PharmacyFinder.API.Data;

public static class NpgsqlEnumConfiguration
{
    public static void ConfigureEnums(NpgsqlDbContextOptionsBuilder options)
    {
        options.MapEnum<UserRole>("user_role");
        options.MapEnum<PharmacyStatus>("pharmacy_status");
        options.MapEnum<PrescriptionStatus>("prescription_status");
        options.MapEnum<ApprovalDecision>("approval_decision");
    }
}
