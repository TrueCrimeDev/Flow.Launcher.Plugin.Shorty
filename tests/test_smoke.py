def test_pytest_runs():
    assert 1 + 1 == 2


def test_tmp_appdata_redirects(tmp_appdata):
    import os
    assert os.environ["APPDATA"] == str(tmp_appdata)
