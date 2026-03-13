using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IVF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationWaf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "waf_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WafRuleId = table.Column<Guid>(type: "uuid", nullable: true),
                    RuleName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RuleGroup = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    ClientIp = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    Country = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    RequestPath = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RequestMethod = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    QueryString = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MatchedPattern = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MatchedValue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ResponseStatusCode = table.Column<int>(type: "integer", nullable: true),
                    Headers = table.Column<string>(type: "jsonb", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ProcessingTimeMs = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_waf_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "waf_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    RuleGroup = table.Column<int>(type: "integer", nullable: false),
                    IsManaged = table.Column<bool>(type: "boolean", nullable: false),
                    UriPathPatterns = table.Column<string>(type: "jsonb", nullable: true),
                    QueryStringPatterns = table.Column<string>(type: "jsonb", nullable: true),
                    HeaderPatterns = table.Column<string>(type: "jsonb", nullable: true),
                    BodyPatterns = table.Column<string>(type: "jsonb", nullable: true),
                    Methods = table.Column<string>(type: "jsonb", nullable: true),
                    IpCidrList = table.Column<string>(type: "jsonb", nullable: true),
                    CountryCodes = table.Column<string>(type: "jsonb", nullable: true),
                    UserAgentPatterns = table.Column<string>(type: "jsonb", nullable: true),
                    MatchType = table.Column<int>(type: "integer", nullable: false),
                    NegateMatch = table.Column<bool>(type: "boolean", nullable: false),
                    Expression = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    RateLimitRequests = table.Column<int>(type: "integer", nullable: true),
                    RateLimitWindowSeconds = table.Column<int>(type: "integer", nullable: true),
                    BlockResponseMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    HitCount = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LastModifiedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_waf_rules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_waf_events_Action",
                table: "waf_events",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_waf_events_Action_CreatedAt",
                table: "waf_events",
                columns: new[] { "Action", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_waf_events_ClientIp",
                table: "waf_events",
                column: "ClientIp");

            migrationBuilder.CreateIndex(
                name: "IX_waf_events_ClientIp_CreatedAt",
                table: "waf_events",
                columns: new[] { "ClientIp", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_waf_events_CreatedAt",
                table: "waf_events",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_waf_events_WafRuleId",
                table: "waf_events",
                column: "WafRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_waf_rules_IsEnabled_Priority",
                table: "waf_rules",
                columns: new[] { "IsEnabled", "Priority" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "waf_events");

            migrationBuilder.DropTable(
                name: "waf_rules");
        }
    }
}
