<?xml version="1.0" encoding="UTF-8" ?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
	<xsl:output method="html"/>
	<xsl:param name="archiveId">BcJ4_XfTO6Rs9qcxJl84IEsXYfKlX6J5XwaUdGQwjqnzDdRP58hsnE1pivZsQz_TU4R11YyTujfIuz1Iqe6ALST99_vNSDs2aZHIMgovWNPISR6x0IP_FNQpR-Cx2DnYk1WxL5HZog</xsl:param>
	<xsl:template match="/">
		<xsl:apply-templates select="//ArchiveList/Archive[ArchiveId=$archiveId]"/>
	</xsl:template>
	<xsl:template match="Archive">
<html>
	<head>
		<title>AWS Archive Details</title>
  </head>
	<body>
		<h1>Details of <xsl:value-of select="substring-after(substring-after(ArchiveDescription, ','), ',')" /></h1>
    <a style="font-size: smaller">
      <xsl:attribute name="href">/<xsl:value-of select="../../VaultName"/>
    </xsl:attribute>Back to Vault <xsl:value-of select="../../VaultName"/></a>
    <h3>Local File Details</h3>
    <table border='0'>
			<tr><th align='left' style='padding-left: 15px'>Path:</th><td align='left'><xsl:value-of select="substring-after(substring-after(ArchiveDescription, ','), ',')" /></td></tr>
			<tr><th align='left' style='padding-left: 15px'>Size:</th><td align='left'><xsl:value-of select="format-number(substring-before(substring-after(ArchiveDescription, ','), ','), '#,##0')" /></td></tr>
			<tr><th align='left' style='padding-left: 15px'>Last Modified:</th><td align='left'><xsl:value-of select="substring-before(ArchiveDescription, ',')" /></td></tr>
    </table>
    <br />
    <br />
    <br />
    <h3>Server File Details</h3>
    <table border='0'>
			<tr><th align='left' style='padding-left: 15px'>Archive ID:</th><td align='left'><xsl:value-of select="ArchiveId" /></td></tr>
			<tr><th align='left' style='padding-left: 15px'>Creation Date:</th><td align='left'><xsl:value-of select="CreationDate" /></td></tr>
			<tr><th align='left' style='padding-left: 15px'>SHA 256 Hash:</th><td align='left'><xsl:value-of select="SHA256TreeHash" /></td></tr>
			<tr><th align='left' style='padding-left: 15px'>Archive Description:</th><td align='left'><xsl:value-of select="ArchiveDescription" /></td></tr>
		</table>
    <xsl:apply-templates select="./Download"/>
  </body>
</html>
  </xsl:template>
  <xsl:template match="Download">
    <br />
    <br />
    <br />
    <h3>Download Details</h3>
		<table border='0'>
			<tr><th align='left'>Scheduled At:</th><td align='right'><xsl:value-of select="ScheduledAt" /></td></tr>
			<tr><th align='left'>Started At:</th><td align='right'><xsl:value-of select="StartedAt" /></td></tr>
			<tr><th align='left'>Completed At:</th><td align='right'><xsl:value-of select="CompletedAt" /></td></tr>
			<tr><th align='left'>Total Bytes:</th><td align='right'><xsl:value-of select="format-number(TotalBytes, '#,##0')" /></td></tr>
			<tr><th align='left'>Bytes Transferred:</th><td align='right'><xsl:value-of select="format-number(TransferredBytes, '#,##0')" /></td></tr>
			<tr><th align='left'>Percent Done:</th><td align='right'><xsl:value-of select="PercentDone" />%</td></tr>
			<tr><th align='left'>Job ID:</th><td align='center'><xsl:value-of select="JobId" /></td></tr>
		</table>
  </xsl:template>
</xsl:stylesheet>

