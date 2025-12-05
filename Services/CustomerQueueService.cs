// Services/CustomerQueueService.cs
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Queueless.Models.Queue;

namespace Queueless.Services
{
    public class CustomerQueueService : ICustomerQueueService
    {
        private readonly string _connectionString;

        public CustomerQueueService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                               ?? throw new InvalidOperationException("Missing DefaultConnection");
        }

        public async Task<CustomerTokenDto> JoinQueueAsync(int businessId, int customerUserId)
        {
            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("Customer_JoinQueue", con)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@BusinessId", businessId);
            cmd.Parameters.AddWithValue("@CustomerUserId", customerUserId);

            await con.OpenAsync();

            using var rdr = await cmd.ExecuteReaderAsync();

            if (!await rdr.ReadAsync())
                throw new Exception("Customer_JoinQueue did not return any rows");

            return MapTokenDto(rdr, includeQueueInfo: true);
        }

        public async Task<List<CustomerTokenDto>> GetActiveTokensAsync(int customerUserId)
        {
            var list = new List<CustomerTokenDto>();

            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("Customer_GetActiveTokens", con)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@CustomerUserId", customerUserId);

            await con.OpenAsync();
            using var rdr = await cmd.ExecuteReaderAsync();

            while (await rdr.ReadAsync())
            {
                list.Add(MapTokenDto(rdr, includeQueueInfo: false));
            }

            return list;
        }

        private CustomerTokenDto MapTokenDto(SqlDataReader rdr, bool includeQueueInfo)
        {
            var dto = new CustomerTokenDto
            {
                TokenId = rdr.GetInt32(rdr.GetOrdinal("TokenId")),
                BusinessId = rdr.GetInt32(rdr.GetOrdinal("BusinessId")),
                CustomerUserId = rdr.GetInt32(rdr.GetOrdinal("CustomerUserId")),
                TokenNumber = rdr.GetInt32(rdr.GetOrdinal("TokenNumber")),
                Status = rdr["Status"] as string ?? string.Empty,
                CreatedOn = rdr.GetDateTime(rdr.GetOrdinal("CreatedOn")),
                BusinessName = rdr["BusinessName"] as string ?? string.Empty,
                Category = rdr["Category"] as string ?? string.Empty,
                Emirate = rdr["Emirate"] as string ?? string.Empty,
                Area = rdr["Area"] as string ?? string.Empty
            };

            if (includeQueueInfo)
            {
                dto.WaitingAhead = rdr["WaitingAhead"] == DBNull.Value
                    ? (int?)null
                    : Convert.ToInt32(rdr["WaitingAhead"]);

                dto.EstimatedWaitMinutes = rdr["EstimatedWaitMinutes"] == DBNull.Value
                    ? (int?)null
                    : Convert.ToInt32(rdr["EstimatedWaitMinutes"]);
            }

            return dto;
        }
        public async Task<CustomerTokenDto?> GetActiveTokenForBusinessAsync(
      int businessId,
      int customerUserId)
        {
            const string sql = @"
SELECT TOP (1)
       t.TokenId,
       t.BusinessId,
       t.CustomerUserId,
       t.TokenNumber,
       t.Status,
       t.CreatedOn,
       b.BusinessName,
       b.Category,
       b.Emirate,
       b.Area
FROM   ServiceQueueToken t
JOIN   BusinessRegistration b
       ON b.BusinessId = t.BusinessId
WHERE  t.BusinessId      = @BusinessId
  AND  t.CustomerUserId  = @CustomerUserId
  AND  CAST(t.CreatedOn AS date) = CAST(GETDATE() AS date)
  AND  t.Status IN ('Waiting','Requested','At counter')   -- active statuses
ORDER BY t.CreatedOn DESC;";

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add(new SqlParameter("@BusinessId", SqlDbType.Int) { Value = businessId });
            cmd.Parameters.Add(new SqlParameter("@CustomerUserId", SqlDbType.Int) { Value = customerUserId });

            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                // no active token for this business + user today
                return null;
            }

            var dto = new CustomerTokenDto
            {
                TokenId = reader.GetInt32(reader.GetOrdinal("TokenId")),
                BusinessId = reader.GetInt32(reader.GetOrdinal("BusinessId")),
                CustomerUserId = reader.GetInt32(reader.GetOrdinal("CustomerUserId")),
                TokenNumber = reader.GetInt32(reader.GetOrdinal("TokenNumber")),
                Status = reader.GetString(reader.GetOrdinal("Status")),
                CreatedOn = reader.GetDateTime(reader.GetOrdinal("CreatedOn")),
                BusinessName = reader.GetString(reader.GetOrdinal("BusinessName")),
                Category = reader.GetString(reader.GetOrdinal("Category")),
                Emirate = reader.GetString(reader.GetOrdinal("Emirate")),
                Area = reader.GetString(reader.GetOrdinal("Area")),

                // You can calculate / fill these properly later if you want
                WaitingAhead = 0,
                EstimatedWaitMinutes = 0
            };

            return dto;
        }
    }
}
