using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PQRS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUpdatedAtToKnowledgeBaseArticle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "KnowledgeBaseArticles",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "KnowledgeBaseArticles");
        }
    }
}
