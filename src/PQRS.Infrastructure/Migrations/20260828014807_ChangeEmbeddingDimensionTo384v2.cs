using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace PQRS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeEmbeddingDimensionTo384v2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Vector>(
                name: "Embedding",
                table: "KnowledgeBaseArticles",
                type: "vector(384)",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector(768)",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Vector>(
                name: "Embedding",
                table: "KnowledgeBaseArticles",
                type: "vector(768)",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector(384)",
                oldNullable: true);
        }
    }
}
