use DBI;
use URI::Escape;
use Time::HiRes qw(time);

$t0 = time;

# Query parameters: "query-url-encoded" zh/hu offset limit
my ($query, $lang, $offset, $limit) = @ARGV;
$query = uri_unescape($query);
$query =~ s/\+/ /;

# "Database" connection to Sphinx
$dbh = DBI->connect('dbi:mysql:host=127.0.0.1;port=9307;mysql_enable_utf8=1') or die "FAIL: Failed to connect via DBI";

# First lookup: either ZH, or HU lower-case unstemmed
$index = 'zh';
if ($lang eq 'hu') { $index = 'hulo'; }
$sth = $dbh->prepare_cached("SELECT id FROM $index WHERE MATCH('\"$query\"') LIMIT $offset, $limit OPTION max_matches=10000");
$sth->execute();
while ($row = $sth->fetchrow_hashref) { print "$row->{id}\n"; }

# If HU lookup, stem each word of query, second lookup in stemmed
$stemQuery = '';
if ($lang eq 'hu') {
  $surfs = $query;
  $surfs =~ s/ /\|/;
  %surfToStem = ();
  # Look up each surface word in stem dictionary
  $sth = $dbh->prepare_cached("SELECT surf, stemmed FROM stemdict WHERE MATCH('$surfs')");
  $sth->execute();
  while ($row = $sth->fetchrow_hashref) { $surfToStem{$row->{surf}} = $row->{stemmed}; }
  @surfarr = split /\|/, $surfs;
  while($surf = shift(@surfarr)) {
    if (length($stemQuery) > 0) { $stemQuery .= ' '; }
    if (exists($surfToStem{$surf})) { $stemQuery .= $surfToStem{$surf}; }
    else { $stemQuery .= $surf; }
  }
  # Look up stemmed query
  $sth = $dbh->prepare_cached("SELECT id, txt FROM hustem WHERE MATCH('\"$stemQuery\"') LIMIT $offset, $limit OPTION max_matches=10000");
  $sth->execute();
  # Dump results after separator
  print "STEMMED $stemQuery\n";
  while ($row = $sth->fetchrow_hashref) { print "$row->{id}\t$row->{txt}\n"; }
}

# Now find total count. ZH, or hu-stem.
$count = 0;
$countQuery = $query;
if ($index eq 'hulo') { 
  $index = 'hustem';
  $countQuery = $stemQuery;
}
$sth = $dbh->prepare_cached("SELECT 1 AS dummy, COUNT(*) c FROM $index WHERE MATCH('\"$countQuery\"') GROUP BY dummy;");
$sth->execute();
while ($row = $sth->fetchrow_hashref) { $count = $row->{c}; }

# Print final report row
$elapsed = time - $t0;
print "COUNT\t$count\t$elapsed\n";
