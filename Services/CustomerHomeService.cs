// Services/CustomerHomeService.cs
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Queueless.Models.Business;

namespace Queueless.Services
{
    public interface ICustomerHomeService
    {
        Task<List<NearbyBusinessDto>> GetNearbyBusinessesAsync(
            decimal lat,
            decimal lon,
            double radiusKm,
            string? category);
    }

    public class CustomerHomeService : ICustomerHomeService
    {
        private readonly string _connectionString;

        public CustomerHomeService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Connection string 'DefaultConnection' not found.");
        }

        public async Task<List<NearbyBusinessDto>> GetNearbyBusinessesAsync(
            decimal lat,
            decimal lon,
            double radiusKm,
            string? category)
        {
            var list = new List<NearbyBusinessDto>();

            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("sp_GetNearbyBusinesses", con);
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue("@CurrentLat", lat);
            cmd.Parameters.AddWithValue("@CurrentLon", lon);
            cmd.Parameters.AddWithValue("@RadiusKm", radiusKm);

            if (string.IsNullOrWhiteSpace(category))
                cmd.Parameters.AddWithValue("@Category", DBNull.Value);
            else
                cmd.Parameters.AddWithValue("@Category", category);

            await con.OpenAsync();

            using var rdr = await cmd.ExecuteReaderAsync();

            while (await rdr.ReadAsync())
            {
                var item = new NearbyBusinessDto
                {
                    BusinessId = rdr.GetInt32(rdr.GetOrdinal("BusinessId")),
                    BusinessName = rdr["BusinessName"] as string ?? string.Empty,
                    Category = rdr["Category"] as string ?? string.Empty,
                    Emirate = rdr["Emirate"] as string ?? string.Empty,
                    Area = rdr["Area"] as string ?? string.Empty,
                    BuildingName = rdr["BuildingName"] as string ?? string.Empty,

                    // proper nullable handling – no "string?" in the `as` cast
                    Landmark = rdr["Landmark"] == DBNull.Value
                        ? null
                        : rdr["Landmark"]!.ToString(),

                    Latitude = rdr.GetFieldValue<decimal>(rdr.GetOrdinal("Latitude")),
                    Longitude = rdr.GetFieldValue<decimal>(rdr.GetOrdinal("Longitude")),
                    DistanceKm = Convert.ToDouble(rdr["DistanceKm"]),
                };

                // WaitingCount is non-nullable int → 0 when null in DB
                item.WaitingCount = rdr["WaitingCount"] == DBNull.Value
                    ? 0
                    : Convert.ToInt32(rdr["WaitingCount"]);

                item.EstimatedWaitMinutes = rdr["EstimatedWaitMinutes"] == DBNull.Value
                    ? (int?)null
                    : Convert.ToInt32(rdr["EstimatedWaitMinutes"]);

                item.AvgTimeMinutes = rdr["AvgTimeMinutes"] == DBNull.Value
                    ? (int?)null
                    : Convert.ToInt32(rdr["AvgTimeMinutes"]);

                list.Add(item);
            }

            return list;
        }
    }
}
