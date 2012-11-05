<?xml version="1.0" encoding="UTF-8" ?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
	<xsl:output method="html"/>
  <xsl:param name="exception" />
	<xsl:template match="/">
	<html>
		<head>
			<title>AWS File Sync</title>
			<meta http-equiv='refresh' content='10' />
		</head>
		<body>
			<h1>AWS File Sync</h1>
      <xsl:if test='string-length($exception) > 0'>
        <h2 style='color:red'>Exception Occurred</h2>
        <h3 style='color:red'>
          <xsl:value-of select='$exception'/>
        </h3>
        <br />
        <br />
      </xsl:if>
			<h3>Last Run Details</h3>
			<table border='0'>
				<tr><th>Last Start Time:</th><td><xsl:value-of select="/VaultSync/StartTime" /></td></tr>
				<tr><th>Last Scheduled Stop:</th><td><xsl:value-of select="/VaultSync/ScheduledStopTime" /></td></tr>
				<tr><th>Last Actual Stop:</th><td><xsl:value-of select="/VaultSync/StopTime" /></td></tr>
				<tr><th>Process ID:</th><td><xsl:value-of select="/VaultSync/ProcessId" /></td></tr>
				<tr><th>Status:</th><td><xsl:value-of select="/VaultSync/RunStatus" /></td></tr>
				<tr><th>Total Vaults:</th><td><xsl:value-of select="format-number(count(//Vault), '#,##0')" /></td></tr>
				<tr><th>Total Archives:</th><td><xsl:value-of select="format-number(count(//Vault/ArchiveList/Archive), '#,##0')" /></td></tr>
			</table>

			<table border='1'>
				<tr>
					<th>Vault Name</th>
					<th>Archive Count</th>
					<th>Pending Upload</th>
					<th>Pending Delete</th>
					<th>Upload Count</th>
					<th>Upload Bytes</th>
					<th>Upload Time</th>
				</tr>
	<xsl:apply-templates select="//Vault"/>
			</table>
		</body>
	</html>	
	
	</xsl:template>

<!-- -=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-= -->

	<xsl:template match="Vault">
		<tr>
			<td><a><xsl:attribute name="href">?Action=VaultDetail&amp;Vault=<xsl:value-of select="VaultName" /></xsl:attribute><xsl:value-of select="VaultName" /></a></td>
			<td align='center'><xsl:value-of select="format-number(count(./ArchiveList/Archive), '#,##0')" /></td>
			<td align='center'><xsl:value-of select="format-number(count(./ToUpload/Archive), '#,##0')" /></td>
			<td align='center'><xsl:value-of select="format-number(count(./ToDelete/Archive), '#,##0')" /></td>
			<td align='center'><xsl:value-of select="format-number(UploadCount, '#,##0')" /></td>
			<td align='center'><xsl:value-of select="format-number(UploadBytes, '#,##0')" /></td>
			<td align='center'><xsl:value-of select="UploadTime" /></td>
		</tr>
	</xsl:template>

</xsl:stylesheet>

