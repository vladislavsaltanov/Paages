using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paages.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNoteFolderRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Notes_FolderId",
                table: "Notes",
                column: "FolderId");

            migrationBuilder.AddForeignKey(
                name: "FK_Notes_Folders_FolderId",
                table: "Notes",
                column: "FolderId",
                principalTable: "Folders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notes_Folders_FolderId",
                table: "Notes");

            migrationBuilder.DropIndex(
                name: "IX_Notes_FolderId",
                table: "Notes");
        }
    }
}
