#!/usr/bin/env pwsh
# The one entry point for planning, building, testing, packing and releasing the Pug.Application.Security
# packages. The GitHub workflows call nothing else, and the same commands work on a developer's machine.
# Adapted from Pug.Sal's build/build.ps1; unlike Pug.Sal, each package here has its own version.
#
#   build.ps1 -Stage plan
#   build.ps1 -Stage test
#   build.ps1 -Stage pack      [-PreviewSuffix preview.<run>]
#   build.ps1 -Stage checksums
#   build.ps1 -Stage push      [-Source <feed>] [-Branch main] [-Remote origin] [-DryRun]   (NUGET_API_KEY in the environment)
#   build.ps1 -Stage release   [-Branch main] [-Remote origin] [-DryRun]                    (GH_TOKEN in the environment)
#
# A version in a project file is the request to release it: plan compares each packable project's
# PackageVersion with nuget.org and plans every version not there yet, in dependency order. It fails
# when a project changed since its last release tag but its version is already on nuget.org (a missed
# bump). The plan lands in <repo>/artifacts/plan.json, which the later stages read, so a release
# publishes exactly what CI planned and packed for the same commit.
#
# Release tags are <PackageId>-v<X.Y.Z>, created by the release stage on the planned commit. A project
# with no such tag yet falls back to the repo-wide v<X.Y.Z> tags used before per-package versions.
#
# pack builds the planned packages into <repo>/artifacts/dist (unless -ArtifactsDir says otherwise) and
# lists them in artifacts/packages.json; dist/ holds exactly what a release publishes. With
# -PreviewSuffix, each planned package is packed as <version>-<suffix> and depends on the previews of
# the planned packages it references (Directory.Build.targets).
#
# The release stage creates a draft GitHub release per package from dist/ (run checksums first), through
# the gh CLI. A draft that already exists is brought in line with dist/; a published release is never
# touched, because its assets are immutable. -DryRun prints what push or release would do and calls
# nothing. -Branch makes push and release refuse to run unless the planned commit is still the tip of
# that branch on -Remote.

[CmdletBinding()]
param(
	[Parameter( Mandatory )]
	[ValidateSet( 'plan', 'test', 'pack', 'checksums', 'push', 'release' )]
	[string] $Stage,

	# pack stage: build previews of the planned packages, e.g. preview.42
	[ValidatePattern( '^[0-9A-Za-z][0-9A-Za-z.-]*$' )]
	[string] $PreviewSuffix,

	# push stage: the feed to push to
	[string] $Source = 'https://api.nuget.org/v3/index.json',

	# push and release stages: the branch whose tip must still be the planned commit
	[string] $Branch,

	# push and release stages: the remote holding -Branch and the release tags
	[string] $Remote = 'origin',

	# push and release stages: print the commands instead of running them
	[switch] $DryRun,

	[string] $ArtifactsDir
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:RepoRoot = ( Resolve-Path ( Join-Path $PSScriptRoot '..' ) ).Path
$script:Solution = Join-Path $script:RepoRoot 'Pug.Application.Security.sln'
$script:NuGetOrg = 'https://api.nuget.org/v3/index.json'

# The Claude Code plugin, if this repo has one, is versioned with this package (see Assert-PluginVersion).
$script:PluginManifest = Join-Path $script:RepoRoot 'claude/pug-application-security/.claude-plugin/plugin.json'
$script:PluginPackage = 'Pug.Application.Security'

$artifacts = $ArtifactsDir ? [IO.Path]::GetFullPath( $ArtifactsDir ) : ( Join-Path $script:RepoRoot 'artifacts' )
$dist = Join-Path $artifacts 'dist'
$planFile = Join-Path $artifacts 'plan.json'
$packagesFile = Join-Path $artifacts 'packages.json'

# Runs a native command and throws on a non-zero exit code. -Capture returns stdout as one string.
# -Secret values are shown as *** wherever the command is echoed. -ReadOnly runs the command even
# under -DryRun, for queries that change nothing.
function Invoke-Native
{
	param(
		[Parameter( Mandatory )] [string] $FilePath, [string[]] $Arguments = @(), [switch] $Capture,
		[string[]] $Secret = @(), [switch] $ReadOnly )

	$shown = ( $Arguments | ForEach-Object { $Secret -contains $_ ? '***' : $_ } ) -join ' '

	if( $DryRun -and -not $ReadOnly -and $Stage -in 'push', 'release' )
	{
		Write-Host "[dry run] $FilePath $shown"
		return $null
	}

	Write-Host "> $FilePath $shown"

	if( $Capture )
	{
		$output = & $FilePath @Arguments | Out-String
	}
	else
	{
		& $FilePath @Arguments | Out-Host
	}

	if( $LASTEXITCODE -ne 0 )
	{
		throw "$FilePath exited with code $LASTEXITCODE"
	}

	if( $Capture )
	{
		return $output.TrimEnd()
	}
}

# Runs git quietly and returns its output lines; for queries whose exit code is the answer.
function Invoke-GitQuery
{
	param( [Parameter( Mandatory )] [string[]] $Arguments )

	$output = & git -C $script:RepoRoot @Arguments 2>$null

	return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Lines = @( $output ) }
}

# A package version as NuGet normalizes it, which is how nuget.org lists it and dotnet pack names the
# file: a zero fourth part is dropped (3.1.0.0 is 3.1.0), leading zeros go, build metadata is ignored and
# the prerelease label is lower-cased.
function ConvertTo-NormalizedVersion
{
	param( [Parameter( Mandatory )] [string] $Version )

	$match = [regex]::Match( $Version.Trim(), '^(?<numbers>\d+(\.\d+){1,3})(-(?<label>[0-9A-Za-z.-]+))?(\+.*)?$' )

	if( -not $match.Success )
	{
		throw "'$Version' is not a NuGet package version"
	}

	$numbers = [version] $match.Groups['numbers'].Value
	$normalized = $numbers.Revision -gt 0 ? $numbers.ToString( 4 ) : "$( $numbers.Major ).$( $numbers.Minor ).$( [Math]::Max( $numbers.Build, 0 ) )"

	if( $match.Groups['label'].Success )
	{
		$normalized += "-$( $match.Groups['label'].Value.ToLowerInvariant() )"
	}

	return $normalized
}

# The packable projects of the solution, each with its package identity and in-repo references.
function Get-PackableProjects
{
	$listing = Invoke-Native -FilePath 'dotnet' -Capture -ReadOnly -Arguments @( 'sln', $script:Solution, 'list' )
	$paths = @( "$listing" -split "`n" | ForEach-Object Trim | Where-Object { $_ -like '*.csproj' } )

	$projects = foreach( $relative in $paths )
	{
		$path = [IO.Path]::GetFullPath( ( Join-Path $script:RepoRoot $relative ) )

		$json = Invoke-Native -FilePath 'dotnet' -Capture -ReadOnly -Arguments @(
			'msbuild', $path, '-nologo', '-getProperty:IsPackable', '-getProperty:PackageId',
			'-getProperty:PackageVersion', '-getProperty:MSBuildProjectName', '-getItem:ProjectReference' )

		$evaluation = $json | ConvertFrom-Json

		if( $evaluation.Properties.IsPackable -ne 'true' )
		{
			continue
		}

		$references = @()

		if( $evaluation.PSObject.Properties[ 'Items' ] -and $evaluation.Items.PSObject.Properties[ 'ProjectReference' ] )
		{
			$references = @( $evaluation.Items.ProjectReference | ForEach-Object { [IO.Path]::GetFullPath( $_.FullPath ) } )
		}

		[pscustomobject]@{
			Path       = $path
			Directory  = Split-Path -Parent $path
			Name       = $evaluation.Properties.MSBuildProjectName
			Id         = $evaluation.Properties.PackageId
			Version    = ConvertTo-NormalizedVersion $evaluation.Properties.PackageVersion
			References = $references
		}
	}

	return @( $projects )
}

# Orders projects so that every project comes after the projects it references.
function Sort-ByDependency
{
	param( [Parameter( Mandatory )] [object[]] $Projects )

	$byPath = @{}
	$Projects | ForEach-Object { $byPath[ $_.Path ] = $_ }

	$sorted = [Collections.Generic.List[object]]::new()
	$visiting = @{}

	function Visit( $project )
	{
		if( $sorted.Contains( $project ) )
		{
			return
		}

		if( $visiting[ $project.Path ] )
		{
			throw "project reference cycle through $( $project.Name )"
		}

		$visiting[ $project.Path ] = $true

		foreach( $reference in $project.References | Where-Object { $byPath.ContainsKey( $_ ) } )
		{
			Visit $byPath[ $reference ]
		}

		$sorted.Add( $project )
	}

	$Projects | Sort-Object Name | ForEach-Object { Visit $_ }

	return $sorted.ToArray()
}

# The versions of a package on nuget.org, unlisted ones included (none for a package never pushed).
function Get-PublishedVersions
{
	param( [Parameter( Mandatory )] [string] $PackageId )

	$uri = "https://api.nuget.org/v3-flatcontainer/$( $PackageId.ToLowerInvariant() )/index.json"
	$response = Invoke-RestMethod -Uri $uri -SkipHttpErrorCheck -StatusCodeVariable status

	if( $status -eq 404 )
	{
		return @()
	}

	if( $status -ne 200 )
	{
		throw "nuget.org answered $status for $uri"
	}

	return @( $response.versions )
}

# The last release of a package: its newest <PackageId>-v<X.Y.Z> tag, or else the newest repo-wide
# v<X.Y.Z> tag from before per-package versions. Null when there is neither.
function Get-LastReleaseTag
{
	param( [Parameter( Mandatory )] [string] $PackageId, [Parameter( Mandatory )] [string[]] $Tags )

	foreach( $pattern in "^$( [regex]::Escape( $PackageId ) )-v(?<version>\d+\.\d+\.\d+)$", '^v(?<version>\d+\.\d+\.\d+)$' )
	{
		$newest = $Tags |
			ForEach-Object { $match = [regex]::Match( $_, $pattern ); if( $match.Success ) { [pscustomobject]@{ Tag = $_; Version = $match.Groups['version'].Value } } } |
			Sort-Object { [version] $_.Version } -Descending |
			Select-Object -First 1

		if( $newest )
		{
			return $newest
		}
	}

	return $null
}

function Get-ReleaseTagName
{
	param( [Parameter( Mandatory )] $Package )

	return "$( $Package.id )-v$( $Package.version )"
}

# The Claude Code plugin, when this repo has one, is versioned with $script:PluginPackage: every bump of
# that package's version bumps plugin.json in the same commit. A repo without the plugin skips the check.
function Assert-PluginVersion
{
	param( [object[]] $Projects )

	if( -not ( Test-Path -LiteralPath $script:PluginManifest ) )
	{
		Write-Host "no plugin manifest at $script:PluginManifest; skipping the plugin version check"
		return
	}

	$pluginVersion = ( Get-Content -Raw $script:PluginManifest | ConvertFrom-Json ).version

	if( -not $Projects )
	{
		$Projects = Get-PackableProjects
	}

	$project = $Projects | Where-Object Id -eq $script:PluginPackage

	if( -not $project )
	{
		throw "the plugin is versioned with $script:PluginPackage, which is not a packable project of the solution"
	}

	if( $pluginVersion -ne $project.Version )
	{
		throw "plugin.json declares version $pluginVersion but $script:PluginPackage declares $( $project.Version ); bump both together"
	}
}

# UTC, 24-hour, so stamps sort in build order.
function Get-BuildStamp
{
	return [DateTime]::UtcNow.ToString( 'yyyyMMddHHmm', [Globalization.CultureInfo]::InvariantCulture )
}

# Stage outputs for the workflow (GITHUB_OUTPUT), echoed for a local run.
function Write-StageOutputs
{
	param( [Parameter( Mandatory )] [System.Collections.IDictionary] $Values )

	foreach( $entry in $Values.GetEnumerator() )
	{
		$value = $entry.Value -is [bool] ? $entry.Value.ToString().ToLowerInvariant() : "$( $entry.Value )"
		Write-Host "$( $entry.Key )=$value"

		if( $env:GITHUB_OUTPUT )
		{
			Add-Content -LiteralPath $env:GITHUB_OUTPUT -Value "$( $entry.Key )=$value"
		}
	}
}

# Markdown for the workflow's job summary (GITHUB_STEP_SUMMARY), skipped on a local run.
function Write-StepSummary
{
	param( [Parameter( Mandatory )] [AllowEmptyString()] [string[]] $Lines )

	if( $env:GITHUB_STEP_SUMMARY )
	{
		Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value ( $Lines -join "`n" )
	}
}

# Works out which packages to release; see the header. Writes plan.json and the stage outputs, and
# throws after listing every problem found.
function New-ReleasePlan
{
	$projects = Sort-ByDependency ( Get-PackableProjects )

	Assert-PluginVersion -Projects $projects

	$commit = ( Invoke-GitQuery @( 'rev-parse', 'HEAD' ) ).Lines[0]
	$tags = ( Invoke-GitQuery @( 'tag', '--list' ) ).Lines
	$byPath = @{}
	$projects | ForEach-Object { $byPath[ $_.Path ] = $_ }

	if( ( Invoke-GitQuery @( 'status', '--porcelain' ) ).Lines )
	{
		Write-Warning "the working tree has uncommitted changes; the plan describes commit $commit only"
	}

	$problems = [Collections.Generic.List[string]]::new()
	$warnings = [Collections.Generic.List[string]]::new()

	$packages = foreach( $project in $projects )
	{
		$published = Get-PublishedVersions $project.Id
		$isPublished = @( $published | Where-Object { $_ -eq $project.Version } ).Count -gt 0
		$last = Get-LastReleaseTag -PackageId $project.Id -Tags $tags
		$changed = $false

		if( $last )
		{
			$relative = [IO.Path]::GetRelativePath( $script:RepoRoot, $project.Directory )
			$changed = ( Invoke-GitQuery @( 'diff', '--quiet', $last.Tag, 'HEAD', '--', $relative ) ).ExitCode -ne 0
		}

		if( $isPublished -and $changed )
		{
			$problems.Add( "$( $project.Id ) changed since $( $last.Tag ) but $( $project.Version ) is already on nuget.org; bump its version" )
		}

		# package validation compares against the last release unless the major version moved on, which is
		# where breaking changes are allowed
		$baseline = $null

		if( -not $isPublished -and $last -and ( $published -contains $last.Version ) )
		{
			$current = [version] ( $project.Version -replace '-.*$', '' )

			if( $current.Major -eq ( [version] $last.Version ).Major )
			{
				$baseline = $last.Version
			}
		}

		[pscustomobject][ordered]@{
			id                 = $project.Id
			name               = $project.Name
			project            = [IO.Path]::GetRelativePath( $script:RepoRoot, $project.Path ).Replace( '\', '/' )
			version            = $project.Version
			planned            = -not $isPublished
			lastReleaseTag     = $last ? $last.Tag : $null
			lastReleaseVersion = $last ? $last.Version : $null
			changedSinceTag    = $changed
			validationBaseline = $baseline
			references         = @( $project.References | Where-Object { $byPath.ContainsKey( $_ ) } | ForEach-Object { $byPath[ $_ ].Id } )
		}
	}

	$packages = @( $packages )
	$planned = @( $packages | Where-Object planned )

	foreach( $package in $packages )
	{
		foreach( $reference in $package.references )
		{
			$target = $packages | Where-Object id -eq $reference

			# a planned major release of a reference, and this package not released against it
			if( $target.planned -and -not $package.planned -and $target.lastReleaseVersion -and
				( [version] ( $target.version -replace '-.*$', '' ) ).Major -gt ( [version] $target.lastReleaseVersion ).Major )
			{
				$warnings.Add( "$( $target.id ) $( $target.version ) is a new major version, but $( $package.id ) is not planned; its released packages still accept it" )
			}
		}
	}

	$plan = [ordered]@{ commit = $commit; packages = $packages }

	$null = New-Item -ItemType Directory -Force -Path $artifacts
	[IO.File]::WriteAllText( $planFile, ( $plan | ConvertTo-Json -Depth 5 ) + "`n" )

	$summary = @( '### Release plan', '', "Commit ``$commit``", '', '| Package | Version | Last release | Changed | Action |', '|---|---|---|---|---|' )

	foreach( $package in $packages )
	{
		$action = $package.planned ? ( $package.validationBaseline ? "release (validated against $( $package.validationBaseline ))" : 'release' ) : 'none'
		$line = "| $( $package.id ) | $( $package.version ) | $( $package.lastReleaseTag ?? '-' ) | $( $package.changedSinceTag ? 'yes' : 'no' ) | $action |"
		$summary += $line
		Write-Host ( $line.Trim( '|', ' ' ) -replace ' \| ', '  ' )
	}

	$summary += @( $warnings | ForEach-Object { "> warning: $_" } ) + @( $problems | ForEach-Object { "> **error:** $_" } )
	Write-StepSummary $summary

	foreach( $warning in $warnings )
	{
		Write-Warning $warning
	}

	Write-StageOutputs ( [ordered]@{ packages = ( @( $planned | ForEach-Object id ) -join ',' ); release = $planned.Count -gt 0 } )

	if( $problems )
	{
		throw "the release plan has problems:`n  $( $problems -join "`n  " )"
	}
}

function Read-JsonFile
{
	param( [Parameter( Mandatory )] [string] $Path, [Parameter( Mandatory )] [string] $Missing )

	if( -not ( Test-Path -LiteralPath $Path ) )
	{
		throw "$Path does not exist; $Missing"
	}

	return Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Read-Plan
{
	return Read-JsonFile -Path $planFile -Missing 'run -Stage plan first'
}

function Read-PackedPackages
{
	return @( ( Read-JsonFile -Path $packagesFile -Missing 'run -Stage pack first' ).packages )
}

# Refuses to publish a commit that is no longer the tip of -Branch: a newer commit has its own run.
function Assert-BranchTip
{
	param( [Parameter( Mandatory )] [string] $Commit )

	if( -not $Branch )
	{
		return
	}

	$listing = Invoke-Native -FilePath 'git' -Capture -ReadOnly -Arguments @( '-C', $script:RepoRoot, 'ls-remote', $Remote, "refs/heads/$Branch" )
	$tip = ( "$listing" -split '\s+' )[0]

	if( $tip -ne $Commit )
	{
		throw "the plan is for $Commit but $Branch on $Remote is at $( $tip ? $tip : 'nothing' ); the newer commit releases itself"
	}
}

# Points the release tag at the planned commit, unless it already does. A tag on another commit is an
# error: that version was released from different code.
function Set-ReleaseTag
{
	param( [Parameter( Mandatory )] [string] $Tag, [Parameter( Mandatory )] [string] $Commit )

	$listing = Invoke-Native -FilePath 'git' -Capture -ReadOnly -Arguments @(
		'-C', $script:RepoRoot, 'ls-remote', '--tags', $Remote, "refs/tags/$Tag", "refs/tags/$Tag^{}" )

	$lines = @( "$listing" -split "`n" | Where-Object { $_.Trim() } )

	if( $lines )
	{
		# an annotated tag lists its commit on the ^{} line
		$peeled = $lines | Where-Object { $_ -like '*^{}' } | Select-Object -First 1
		$target = ( ( $peeled ?? $lines[0] ) -split '\s+' )[0]

		if( $target -ne $Commit )
		{
			throw "tag $Tag already exists on $target, not on the planned commit $Commit"
		}

		return
	}

	Invoke-Native -FilePath 'git' -Arguments @( '-C', $script:RepoRoot, 'tag', $Tag, $Commit )
	Invoke-Native -FilePath 'git' -Arguments @( '-C', $script:RepoRoot, 'push', $Remote, "refs/tags/$Tag" )
}

# Creates the draft release for one package from its files in dist/, or replaces the assets of the
# draft a previous run left. A published release is an error: its assets cannot be replaced.
function Publish-GitHubRelease
{
	param( [Parameter( Mandatory )] $Package, [Parameter( Mandatory )] [string] $Commit )

	$tag = Get-ReleaseTagName $Package
	$sumsFile = Join-Path $dist 'SHA256SUMS'

	if( -not ( Test-Path -LiteralPath $sumsFile ) )
	{
		throw "$dist has no SHA256SUMS; run -Stage checksums first"
	}

	$files = @( $Package.files | ForEach-Object { Get-Item -LiteralPath ( Join-Path $dist $_ ) } )

	# this package's lines of SHA256SUMS, as the release's own SHA256SUMS
	$releaseDir = Join-Path $artifacts "release/$tag"
	$null = New-Item -ItemType Directory -Force -Path $releaseDir
	$sums = @( Get-Content -LiteralPath $sumsFile | Where-Object { ( $_ -split '\s+', 2 )[1] -in $Package.files } )
	[IO.File]::WriteAllText( ( Join-Path $releaseDir 'SHA256SUMS' ), ( $sums -join "`n" ) + "`n" )
	$files += Get-Item -LiteralPath ( Join-Path $releaseDir 'SHA256SUMS' )

	Set-ReleaseTag -Tag $tag -Commit $Commit

	$title = "$( $Package.id ) $( $Package.version )"
	$notes = "$title. The package is published to nuget.org; verify these copies against SHA256SUMS."
	$paths = @( $files | ForEach-Object FullName )

	# the releases API lists drafts, which a lookup by tag does not reliably find; the tag is built from
	# a package id and version, so it is safe inside the jq program
	$listing = Invoke-Native -FilePath 'gh' -Capture -Arguments @(
		'api', '--paginate', 'repos/{owner}/{repo}/releases?per_page=100', '--jq',
		".[] | select( .tag_name == `"$tag`" ) | { draft, assets: [ .assets[].name ] } | tojson" )

	$found = @( "$listing" -split "`n" | Where-Object { $_.Trim() } | ForEach-Object { $_ | ConvertFrom-Json } )

	if( $found | Where-Object { -not $_.draft } )
	{
		throw "release $tag is already published and its assets are immutable; delete the release (not the tag) or release a new version"
	}

	if( -not $found )
	{
		$null = Invoke-Native -FilePath 'gh' -Arguments ( @(
			'release', 'create', $tag, '--draft', '--verify-tag', '--title', $title, '--notes', $notes ) + $paths )

		return
	}

	$null = Invoke-Native -FilePath 'gh' -Arguments @( 'release', 'edit', $tag, '--title', $title, '--notes', $notes )

	foreach( $asset in @( $found | ForEach-Object { $_.assets } | Where-Object { $_ -and $files.Name -notcontains $_ } ) )
	{
		$null = Invoke-Native -FilePath 'gh' -Arguments @( 'release', 'delete-asset', $tag, $asset, '--yes' )
	}

	$null = Invoke-Native -FilePath 'gh' -Arguments ( @( 'release', 'upload', $tag, '--clobber' ) + $paths )
}

switch( $Stage )
{
	'plan'
	{
		New-ReleasePlan
	}

	# The solution builds and the tests pass. Not -warnaserror, unlike Pug.Sal: the older target
	# frameworks pull in packages with known vulnerabilities (NU1902-NU1904), and obsolete members are
	# still used on purpose (CS0618).
	'test'
	{
		Assert-PluginVersion
		Invoke-Native -FilePath 'dotnet' -Arguments @( 'build', $script:Solution, '-c', 'Release', '-m:1', '-nologo' )

		Invoke-Native -FilePath 'dotnet' -Arguments @( 'test', $script:Solution, '-c', 'Release', '--no-build', '-m:1', '-nologo' )
	}

	# The planned packages, each at its own version, with symbol packages and package validation.
	'pack'
	{
		$plan = Read-Plan
		$planned = @( $plan.packages | Where-Object planned )

		if( Test-Path $dist )
		{
			Remove-Item -Recurse -Force $dist
		}

		$null = New-Item -ItemType Directory -Force -Path $dist
		$packed = @()

		$plannedNames = "|$( ( $planned | ForEach-Object name ) -join '|' )|"

		foreach( $package in $planned )
		{
			$version = $PreviewSuffix ? "$( $package.version )-$PreviewSuffix" : $package.version

			$arguments = @(
				'pack', ( Join-Path $script:RepoRoot $package.project ), '-c', 'Release', '-m:1', '-nologo', '-o', $dist,
				'-p:IncludeSymbols=true', '-p:SymbolPackageFormat=snupkg', '-p:ContinuousIntegrationBuild=true' )

			if( $PreviewSuffix )
			{
				$arguments += @( "-p:PreviewSuffix=$PreviewSuffix", "-p:PlannedProjects=$plannedNames" )
			}

			if( $package.validationBaseline )
			{
				# for this project only (Directory.Build.props), not for the projects it references
				$arguments += @( "-p:ValidationBaselineProject=$( $package.name )", "-p:ValidationBaselineVersion=$( $package.validationBaseline )" )
			}

			Invoke-Native -FilePath 'dotnet' -Arguments $arguments

			$files = @( "$( $package.id ).$version.nupkg", "$( $package.id ).$version.snupkg" ) |
				Where-Object { Test-Path -LiteralPath ( Join-Path $dist $_ ) }

			if( $files -notcontains "$( $package.id ).$version.nupkg" )
			{
				throw "packing $( $package.project ) did not produce $( $package.id ).$version.nupkg"
			}

			$packed += [ordered]@{ id = $package.id; version = $version; files = @( $files ) }
		}

		$unexpected = @( Get-ChildItem -File -Path $dist | Where-Object { $_.Name -notin $packed.files } )

		if( $unexpected )
		{
			throw "dist holds packages the plan does not: $( $unexpected.Name -join ', ' )"
		}

		[IO.File]::WriteAllText( $packagesFile, ( [ordered]@{ commit = $plan.commit; packages = $packed } | ConvertTo-Json -Depth 5 ) + "`n" )

		Write-Host "packed $( $packed.Count ) packages into ${dist}: $( ( $packed | ForEach-Object { "$( $_.id ) $( $_.version )" } ) -join ', ' )"
	}

	'checksums'
	{
		$sums = Get-ChildItem -File -Path $dist |
			Where-Object Name -ne 'SHA256SUMS' |
			Sort-Object Name |
			ForEach-Object { "$( ( Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName ).Hash.ToLowerInvariant() )  $( $_.Name )" }

		[IO.File]::WriteAllText( ( Join-Path $dist 'SHA256SUMS' ), ( $sums -join "`n" ) + "`n" )
	}

	# Pushes the packed packages in dependency order. On nuget.org each .snupkg goes with its .nupkg;
	# other feeds get the .nupkg only. A version already there is skipped, so a re-run after a partial
	# push completes it.
	'push'
	{
		$packages = Read-PackedPackages

		if( -not $packages )
		{
			Write-Host 'nothing to push: the plan releases no packages'
			return
		}

		Assert-BranchTip -Commit ( Read-Plan ).commit

		if( -not $DryRun -and -not $env:NUGET_API_KEY )
		{
			throw 'stage push needs NUGET_API_KEY in the environment'
		}

		$key = $DryRun ? 'NUGET_API_KEY' : $env:NUGET_API_KEY

		foreach( $package in $packages )
		{
			$nupkg = Join-Path $dist ( $package.files | Where-Object { $_ -like '*.nupkg' } )

			$arguments = @( 'nuget', 'push', $nupkg, '--api-key', $key, '--source', $Source, '--skip-duplicate' )

			if( $Source -ne $script:NuGetOrg )
			{
				$arguments += '--no-symbols'
			}

			Invoke-Native -FilePath 'dotnet' -Secret $key -Arguments $arguments
		}
	}

	'release'
	{
		$packages = Read-PackedPackages

		if( -not $packages )
		{
			Write-Host 'nothing to release: the plan releases no packages'
			return
		}

		$preview = @( $packages | Where-Object { $_.version -like '*-*' } )

		if( $preview )
		{
			throw "previews are not released on GitHub: $( ( $preview | ForEach-Object { "$( $_.id ) $( $_.version )" } ) -join ', ' )"
		}

		$commit = ( Read-Plan ).commit
		Assert-BranchTip -Commit $commit

		foreach( $package in $packages )
		{
			Publish-GitHubRelease -Package $package -Commit $commit
		}
	}
}
