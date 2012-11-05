<?xml version="1.0" encoding="UTF-8" ?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
	<xsl:output method="html"/>
	<xsl:param name="vaultName">Family_Pictures</xsl:param>
	<xsl:template match="/">
		<xsl:apply-templates select="/VaultSync/Vaults/Vault[VaultName=$vaultName]"/>
	</xsl:template>
	<xsl:template match="Vault">
<html>
	<head>
		<title>AWS Vault Details</title>
  </head>
	<body>
		<h1>Details for <xsl:value-of select="VaultName" /></h1>
    <a style="font-size: smaller" href="/">Back</a><br />
		<a style="font-size: smaller" href="#PendingUpload">Pending Uploads</a>&#160;&#160;&#160;&#160;&#160;
		<a style="font-size: smaller" href="#UploadError">Upload Errors</a>&#160;&#160;&#160;&#160;&#160;
		<a style="font-size: smaller" href="#PendingDelete">Pending Delete</a>&#160;&#160;&#160;&#160;&#160;
		<a style="font-size: smaller" href="#DeleteError">Delete Errors</a>&#160;&#160;&#160;&#160;&#160;
		<a style="font-size: smaller" href="#Archives">Archives</a><br />
    <br />
    <br />
    <table border='0'>
			<tr><th align='left'>Local Folder</th><td align='right'><xsl:value-of select="LocalFolder" /></td></tr>
			<tr><th align='left'>Archive Count</th><td align='right'><xsl:value-of select="format-number(count(./ArchiveList/Archive), '#,##0')" /></td></tr>
			<tr><th align='left'>Pending Upload</th><td align='right'><xsl:value-of select="format-number(count(./ToUpload/Archive), '#,##0')" /></td></tr>
			<tr><th align='left'>Pending Delete</th><td align='right'><xsl:value-of select="format-number(count(./ToDelete/Archive), '#,##0')" /></td></tr>
			<tr><th align='left'>Upload Count</th><td align='right'><xsl:value-of select="format-number(UploadCount, '#,##0')" /></td></tr>
			<tr><th align='left'>Upload Bytes</th><td align='right'><xsl:value-of select="format-number(UploadBytes, '#,##0')" /></td></tr>
			<tr><th align='left'>Upload Time</th><td align='right'><xsl:value-of select="UploadTime" /></td></tr>
			<tr><th align='left'>Download Count</th><td align='right'><xsl:value-of select="format-number(DownloadCount, '#,##0')" /></td></tr>
			<tr><th align='left'>Download Bytes</th><td align='right'><xsl:value-of select="format-number(DownloadBytes, '#,##0')" /></td></tr>
			<tr><th align='left'>Download Time</th><td align='right'><xsl:value-of select="DownloadTime" /></td></tr>
			<tr><th align='left'>Delete Count</th><td align='right'><xsl:value-of select="format-number(DeleteCount, '#,##0')" /></td></tr>
			<tr><th align='left'>Delete Time</th><td align='right'><xsl:value-of select="DeleteTime" /></td></tr>
			<tr><th align='left'>Loaded From File</th><td align='center'><xsl:value-of select="LoadedFromFile" /></td></tr>
			<tr><th align='left'>Loaded From Server</th><td align='center'><xsl:value-of select="LoadedFromServer" /></td></tr>
		</table>
		<br /><br />
		<h3><a name="PendingUpload" />Pending Uploads</h3>
		<table border='1'>
			<tr>
				<th>Path</th>
				<th>Last Modified</th>
				<th>Size</th>
			</tr>
			<xsl:apply-templates select="./ToUpload/Archive" mode="PendingUpload" />
		</table>
		<br /><br />
		<h3><a name="UploadError" />Upload Errors</h3>
		<table border='1'>
			<tr>
				<th>Path</th>
				<th>Exception</th>
			</tr>
			<xsl:apply-templates select="./UploadErrors/ArchiveException" mode="UploadErrors" />
		</table>
		<br /><br />
		<h3><a name="PendingDelete" />Pending Deletes</h3>
		<table border='1'>
			<tr>
				<th>Path</th>
				<th>Size</th>
				<th>Archive ID</th>
			</tr>
			<xsl:apply-templates select="./ToDelete/Archive" mode="PendingDelete" />
		</table>
		<br /><br />
		<h3><a name="DeleteError" />Delete Errors</h3>
		<table border='1'>
			<tr>
				<th>Path</th>
				<th>Archive ID</th>
				<th>Exception</th>
			</tr>
			<xsl:apply-templates select="./DeleteErrors/ArchiveException" mode="DeleteErrors" />
		</table>
		<br /><br />
		<h3><a name="Archives" />Archives</h3>
		<table border='1'>
			<tr>
        <th>Path</th>
				<th>Size</th>
        <th>Archive ID</th>
      </tr>
			<xsl:apply-templates select="./ArchiveList/Archive" mode="ArchiveList" />
		</table>
	</body>
</html>
	</xsl:template>
	<xsl:template match="Archive" mode="PendingUpload">
		<tr>
			<td><xsl:value-of select="substring-after(substring-after(ArchiveDescription, ','), ',')" /></td>
			<td><xsl:value-of select="substring-before(ArchiveDescription, ',')" /></td>
			<td align='right'><xsl:value-of select="format-number(substring-before(substring-after(ArchiveDescription, ','), ','), '#,##0')" /></td>
		</tr>
	</xsl:template>
	<xsl:template match="ArchiveException" mode="UploadErrors">
		<tr>
			<td><xsl:value-of select="substring-after(substring-after(Archive/ArchiveDescription, ','), ',')" /></td>
			<td><pre><xsl:value-of select="Exception" /></pre></td>
		</tr>
	</xsl:template>
	<xsl:template match="Archive" mode="PendingDelete">
		<tr>
			<td><a><xsl:attribute name="href">/<xsl:value-of select="../../VaultName" />/<xsl:value-of select="ArchiveId" /></xsl:attribute><xsl:value-of select="substring-after(substring-after(ArchiveDescription, ','), ',')" /></a></td>
			<td align='right'><xsl:value-of select="format-number(substring-before(substring-after(ArchiveDescription, ','), ','), '#,##0')" /></td>
			<td align='center'><xsl:value-of select="substring(ArchiveId, 0, 40)" /></td>
		</tr>
	</xsl:template>
  <xsl:template match="Archive" mode="ArchiveList">
    <tr>
      <xsl:if test="count(Download) = 1">
        <xsl:if test="Download/PercentComplete &lt; 100">
          <xsl:attribute name="style">background-color: lightgreen</xsl:attribute>
        </xsl:if>
      </xsl:if>
      <td><a><xsl:attribute name="href">/<xsl:value-of select="../../VaultName" />/<xsl:value-of select="ArchiveId" /></xsl:attribute><xsl:value-of select="substring-after(substring-after(ArchiveDescription, ','), ',')" /></a></td>
      <td align='right'><xsl:value-of select="format-number(substring-before(substring-after(ArchiveDescription, ','), ','), '#,##0')" /></td>
      <td align='center'><xsl:value-of select="substring(ArchiveId, 0, 40)" /></td>
    </tr>
  </xsl:template>
  <xsl:template match="ArchiveException" mode="DeleteErrors">
		<tr>
			<td><xsl:value-of select="substring-after(substring-after(Archive/ArchiveDescription, ','), ',')" /></td>
			<td align='center'><xsl:value-of select="substring(ArchiveId, 0, 40)" /></td>
			<td><pre><xsl:value-of select="Exception" /></pre></td>
		</tr>
	</xsl:template>
</xsl:stylesheet>

