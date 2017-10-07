use DBI;
use URI::Escape;
use Time::HiRes qw(time);

my ($query, $lang, $offset, $limit) = @ARGV;
$query = uri_unescape($query);
$index = 'zh';
if ($lang eq 'hulo') { $index = 'hulo'; }
if ($lang eq 'hustem') { $index = 'hustem'; }

$t0 = time;
$dbh = DBI->connect('dbi:mysql:host=127.0.0.1;port=9306;mysql_enable_utf8=1') or die "FAIL: Failed to connect via DBI";

$sth = $dbh->prepare_cached("SELECT 1 AS dummy, COUNT(*) c FROM $index WHERE MATCH('\"$query\"') GROUP BY dummy;");
$sth->execute();
while ($row = $sth->fetchrow_hashref) {
  $count = $row->{c};
  print "COUNT: $count\n";
}
$elapsed = time - $t0;
#print "Elapsed time: $elapsed\n";

$sth = $dbh->prepare_cached("SELECT id FROM $index WHERE MATCH('\"$query\"') LIMIT $offset, $limit OPTION max_matches=10000");
$sth->execute();
while ($row = $sth->fetchrow_hashref) {
  print "$row->{id}\n";
}
$elapsed = time - $t0;
#print "Elapsed time: $elapsed\n";
