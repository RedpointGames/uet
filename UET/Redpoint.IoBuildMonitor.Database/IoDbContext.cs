using Io.Database.Entities;
using Io.Database.Views;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;

namespace Io.Database
{
    [RequiresUnreferencedCode("EF Core is not compatible with trimming.")]
    public class IoDbContext : DbContext
    {
        public DbSet<PipelineEntity> Pipelines { get; set; }
        public DbSet<MergeRequestEntity> MergeRequests { get; set; }
        public DbSet<UserEntity> Users { get; set; }
        public DbSet<ProjectEntity> Projects { get; set; }
        public DbSet<CommitEntity> Commits { get; set; }
        public DbSet<RunnerEntity> Runners { get; set; }
        public DbSet<BuildEntity> Builds { get; set; }
        public DbSet<BuildStatusChangeEntity> BuildStatusChanges { get; set; }
        public DbSet<WebhookEventEntity> WebhookEvents { get; set; }
        public DbSet<TestEntity> Tests { get; set; }
        public DbSet<TestLogEntity> TestLogs { get; set; }
        public DbSet<UtilizationMinuteEntity> UtilizationMinutes { get; set; }
        public DbSet<UtilizationInvalidationEntity> UtilizationInvalidation { get; set; }
        public DbSet<UtilizationBlockEntity> UtilizationBlocks { get; set; }

        public DbSet<BuildEstimations> BuildEstimations { get; set; }
        public DbSet<PipelineEstimations> PipelineEstimations { get; set; }
        public DbSet<TimestampedBuilds> TimestampedBuilds { get; set; }
        public DbSet<DesiredCapacityCalculations> DesiredCapacityCalculations { get; set; }
        public DbSet<ProjectHealths> ProjectHealths { get; set; }

#pragma warning disable CS8618
        public IoDbContext(DbContextOptions options) : base(options)
        {
        }
#pragma warning restore CS8618

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);

            var pipelineEntity = modelBuilder.Entity<PipelineEntity>();
            pipelineEntity
                .HasMany(x => x.Builds)
                .WithOne(x => x.Pipeline);

            modelBuilder.Entity<MergeRequestEntity>();
            modelBuilder.Entity<UserEntity>();
            modelBuilder.Entity<ProjectEntity>();
            modelBuilder.Entity<CommitEntity>();

            var runnerEntity = modelBuilder.Entity<RunnerEntity>();
            runnerEntity
                .HasMany(x => x.Builds)
                .WithOne(x => x.Runner)
                .HasForeignKey(x => x.RunnerId);

            var buildEntity = modelBuilder.Entity<BuildEntity>();
            buildEntity
                .HasOne(x => x.DownstreamPipeline)
                .WithOne(x => x.UpstreamBuild)
                .HasForeignKey<BuildEntity>(x => x.DownstreamPipelineId);

            modelBuilder.Entity<BuildEstimations>(eb =>
            {
                eb.HasNoKey();
                eb.ToView("BuildEstimations");
                eb.HasOne(x => x.Build).WithOne().HasForeignKey<BuildEstimations>(x => x.BuildId);
            });
            modelBuilder.Entity<PipelineEstimations>(eb =>
            {
                eb.HasNoKey();
                eb.ToView("PipelineEstimations");
                eb.HasOne(x => x.Pipeline).WithOne().HasForeignKey<PipelineEstimations>(x => x.PipelineId);
            });

            var webhookEventEntity = modelBuilder.Entity<WebhookEventEntity>();
            webhookEventEntity
                .Property(x => x.CreatedAt)
                .HasDefaultValueSql("now() at time zone 'utc'");
            webhookEventEntity
                .Property(x => x.Data)
                .HasColumnType("json");

            var testEntity = modelBuilder.Entity<TestEntity>();
            testEntity.Property(x => x.LookupId)
                .HasComputedColumnSql("\"BuildId\"::text || ':' || \"FullName\" || ':' || \"Platform\" || ':' || \"GauntletInstance\"", stored: true)
                .ValueGeneratedOnAdd();
            testEntity.HasOne(x => x.Build)
                .WithMany(x => x.Tests)
                .HasForeignKey(x => x.BuildId);
            testEntity.HasIndex(x => x.LookupId).IsUnique();
            testEntity.Property(x => x.DurationEstimationHash)
                .HasComputedColumnSql("\"FullName\" || ':' || \"Platform\"", stored: true)
                .ValueGeneratedOnAdd();

            var testLogEntity = modelBuilder.Entity<TestLogEntity>();
            testLogEntity.HasOne(x => x.Test)
                .WithMany();

            var buildStatusChangeEntity = modelBuilder.Entity<BuildStatusChangeEntity>();
            buildStatusChangeEntity
                .HasOne(x => x.Build)
                .WithMany();

            modelBuilder.Entity<TimestampedBuilds>(eb =>
            {
                eb.HasNoKey();
                eb.ToView("TimestampedBuilds");
                eb.HasOne(x => x.Build).WithOne().HasForeignKey<TimestampedBuilds>(x => x.BuildId);
            });

            var utilizationEntity = modelBuilder.Entity<UtilizationMinuteEntity>();
            utilizationEntity.HasKey(nameof(UtilizationMinuteEntity.Timestamp), nameof(UtilizationMinuteEntity.RunnerTag));
            utilizationEntity.HasIndex(x => x.Timestamp);
            utilizationEntity.HasIndex(x => x.RunnerTag);

            modelBuilder.Entity<DesiredCapacityCalculations>(eb =>
            {
                eb.HasNoKey();
                eb.ToView("DesiredCapacityCalculations");
            });

            modelBuilder.Entity<ProjectHealths>(eb =>
            {
                eb.HasNoKey();
                eb.ToView("ProjectHealths");
                eb.HasOne(x => x.Project).WithOne().HasForeignKey<ProjectHealths>(x => x.ProjectId);
                eb.HasOne(x => x.Pipeline).WithOne().HasForeignKey<ProjectHealths>(x => x.PipelineId);
            });

            modelBuilder.Entity<UtilizationBlockEntity>(eb =>
            {
                eb.HasKey(
                    nameof(UtilizationBlockEntity.Week),
                    nameof(UtilizationBlockEntity.DayInWeek),
                    nameof(UtilizationBlockEntity.HourQuarter),
                    nameof(UtilizationBlockEntity.RunnerId));
                eb.HasIndex(x => x.Week);
                eb.HasIndex(x => x.RunnerId);
            });
        }
    }
}
