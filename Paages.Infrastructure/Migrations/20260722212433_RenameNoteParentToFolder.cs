using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paages.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameNoteParentToFolder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ParentId",
                table: "Notes",
                newName: "FolderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FolderId",
                table: "Notes",
                newName: "ParentId");
        }
    }
}
