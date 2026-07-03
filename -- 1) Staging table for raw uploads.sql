-- 1) Staging table for raw uploads
CREATE TABLE ResultsStaging (
  Id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
  UploadId UNIQUEIDENTIFIER NOT NULL,
  SourceSystem NVARCHAR(64) NULL,
  RowNumber INT NULL,
  RawPayload NVARCHAR(MAX) NULL,
  AcademicSessionId UNIQUEIDENTIFIER NULL,
  CourseId UNIQUEIDENTIFIER NULL,
  ExternalStudentId NVARCHAR(128) NULL,
  StudentMatchHint NVARCHAR(256) NULL,
  AssessmentType NVARCHAR(64) NULL,
  Marks DECIMAL(9,4) NULL,
  AttemptNumber INT NULL,
  Fingerprint CHAR(64) NULL, -- SHA256 hex
  MappingStatus NVARCHAR(32) NOT NULL DEFAULT 'Pending', -- Pending|Mapped|Failed|Ignored|Processed
  MappingReason NVARCHAR(512) NULL,
  ProcessedAtUtc DATETIME2 NULL,
  CreatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE INDEX IX_ResultsStaging_UploadId ON ResultsStaging(UploadId);
CREATE INDEX IX_ResultsStaging_Fingerprint ON ResultsStaging(Fingerprint);

-- 2) Canonical results table
CREATE TABLE Results (
  Id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
  StudentId UNIQUEIDENTIFIER NOT NULL,
  CourseOfferingId UNIQUEIDENTIFIER NOT NULL,
  AssessmentId UNIQUEIDENTIFIER NULL,
  AssessmentType NVARCHAR(64) NULL,
  Marks DECIMAL(9,4) NOT NULL,
  AttemptNumber INT NULL,
  SourceSystem NVARCHAR(64) NULL,
  UploadId UNIQUEIDENTIFIER NULL,
  Fingerprint CHAR(64) NOT NULL,
  CreatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
  UpdatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

-- Unique fingerprint to prevent duplicates across uploads
CREATE UNIQUE INDEX UX_Results_Fingerprint ON Results(Fingerprint);

-- FKs (optional, add if you want relational guarantees)
-- ALTER TABLE Results ADD CONSTRAINT FK_Results_Students FOREIGN KEY (StudentId) REFERENCES Users(Id);
-- ALTER TABLE Results ADD CONSTRAINT FK_Results_Offerings FOREIGN KEY (CourseOfferingId) REFERENCES CourseOfferings(Id);

-- 3) Enforce CourseOffering uniqueness to avoid concurrent duplicate creation
CREATE UNIQUE INDEX UX_CourseOfferings_UniqueKey ON CourseOfferings (CourseId, ProgramId, LevelId, AcademicSessionId);