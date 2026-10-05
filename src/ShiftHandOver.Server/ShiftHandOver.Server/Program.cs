
using Microsoft.EntityFrameworkCore;
using ShiftHandOver.Server.Models;
using ShiftHandOver.Server.Repository;

namespace ShiftHandOver.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            //Add DbContext to the service container
            builder.Services.AddDbContext<ShiftHandoverDbContext>(option => 
            option.UseSqlServer(builder.Configuration.GetConnectionString("DBContext")));

            builder.Services.AddScoped<IUserRepository, UserRepository>();
            builder.Services.AddScoped<IBranchRepository, BranchRepository>();
            builder.Services.AddScoped<IShiftRepository, ShiftRepository>();

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Tự động kiểm tra và nâng cấp cấu trúc Database nếu thiếu cột (Không làm mất dữ liệu cũ)
            using (var scope = app.Services.CreateScope())
            {
                try
                {
                    var db = scope.ServiceProvider.GetRequiredService<ShiftHandoverDbContext>();
                    db.Database.ExecuteSqlRaw(@"
                        IF NOT EXISTS (
                            SELECT 1 FROM sys.columns 
                            WHERE object_id = OBJECT_ID('dbo.Shifts') AND name = 'ActiveChannels'
                        )
                        BEGIN
                            ALTER TABLE dbo.Shifts ADD ActiveChannels NVARCHAR(100) NULL;
                        END

                        IF NOT EXISTS (
                            SELECT 1 FROM sys.columns 
                            WHERE object_id = OBJECT_ID('dbo.BranchBanks') AND name = 'ImageUrl'
                        )
                        BEGIN
                            ALTER TABLE dbo.BranchBanks ADD ImageUrl NVARCHAR(500) NULL;
                        END

                        IF NOT EXISTS (
                            SELECT 1 FROM sys.columns 
                            WHERE object_id = OBJECT_ID('dbo.PosConfigs') AND name = 'ImageUrl'
                        )
                        BEGIN
                            ALTER TABLE dbo.PosConfigs ADD ImageUrl NVARCHAR(500) NULL;
                        END
                    ");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Cảnh báo kiểm tra cấu trúc DB: " + ex.Message);
                }
            }

            // Đảm bảo thư mục uploads tồn tại để phục vụ ảnh tĩnh
            var uploadsPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "uploads");
            if (!Directory.Exists(uploadsPath))
            {
                Directory.CreateDirectory(uploadsPath);
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseStaticFiles();
            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
